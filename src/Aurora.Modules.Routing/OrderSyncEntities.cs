using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

internal static class OrderSyncEntities
{
    internal const string Select = """
        SELECT o.revision::bigint AS Revision,o.deleted_at IS NOT NULL AS Deleted,
          ((o.details || jsonb_build_object('Id',o.id,'ScheduledAt',o.scheduled_at,'Customer',o.customer,'City',o.city,'Status',o.status,
            'TerminalCode',COALESCE(t.code,o.details->>'TerminalCode'),'CustomerCode',COALESCE(c.code,o.details->>'CustomerCode')))
            - 'SourceId' - 'Revision' - 'TerminalId' - 'CustomerId' - 'OptimizationDraftId' - 'ManifestId' - 'ManifestNumber' - 'StopNumber')::text AS Json
        FROM aurora_order o LEFT JOIN aurora_terminal t ON (t.tenant_id,t.id)=(o.tenant_id,o.terminal_id)
        LEFT JOIN aurora_customer c ON (c.tenant_id,c.id)=(o.tenant_id,o.customer_id)
        WHERE o.tenant_id=@tenantId AND o.source_id=@sourceId AND o.id=@id
        """;

    internal static (Guid SourceId,string Id) Key(string localId)
    {
        var separator=localId.IndexOf('/');
        if(separator<1 || !Guid.TryParse(localId[..separator],out var source) || separator==localId.Length-1)
            throw new FormatException("Invalid local order identity.");
        return(source,localId[(separator+1)..]);
    }

    public static async Task<SyncEntity?> Read(Guid tenantId,string localId,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var (sourceId,id)=Key(localId);
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(Select+" FOR UPDATE OF o",new{tenantId,sourceId,id},tx,cancellationToken:ct));
        return row is null?null:new(localId,row.Revision,JsonNode.Parse(row.Json)!.AsObject(),row.Deleted);
    }

    public static async Task<SyncEntity> Write(Guid tenantId,string? localId,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var normalized=(JsonObject)data.DeepClone();
        foreach(var key in new[]{"SourceId","Revision","TerminalId","CustomerId","OptimizationDraftId","ManifestId","ManifestNumber","StopNumber"}) normalized.Remove(key);
        var item=normalized.Deserialize<TmsOrder>()??throw new FormatException("Invalid incoming order.");
        item.Validate();
        Guid sourceId;
        if(localId is null)
            sourceId=await WorkspaceOrderSource.Get(db,tenantId,item.TerminalCode,tx,ct);
        else
        {
            var key=Key(localId);sourceId=key.SourceId;
            if(key.Id!=item.Id)throw new FormatException("An imported order cannot change its identity.");
        }
        var terminalId=await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT id FROM aurora_terminal WHERE tenant_id=@tenantId AND code=@TerminalCode AND deleted_at IS NULL",new{tenantId,item.TerminalCode},tx,cancellationToken:ct));
        var customerId=await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT id FROM aurora_customer WHERE tenant_id=@tenantId AND code=@CustomerCode AND deleted_at IS NULL",new{tenantId,item.CustomerCode},tx,cancellationToken:ct));
        if(deleted && await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_manifest_order WHERE tenant_id=@tenantId AND source_id=@sourceId AND order_id=@Id)",new{tenantId,sourceId,item.Id},tx,cancellationToken:ct)))
            throw new FormatException("Remove the order from its manifest before synchronizing its deletion.");
        var sql=localId is null?"""
            INSERT INTO aurora_order(tenant_id,source_id,id,scheduled_at,customer,city,status,terminal_id,customer_id,details,deleted_at)
            VALUES(@tenantId,@sourceId,@Id,@scheduled,@Customer,@City,@Status,@terminalId,@customerId,CAST(@json AS jsonb),CASE WHEN @deleted THEN now() END)
            ON CONFLICT(tenant_id,source_id,id) DO UPDATE SET scheduled_at=excluded.scheduled_at,customer=excluded.customer,city=excluded.city,
              status=excluded.status,terminal_id=excluded.terminal_id,customer_id=excluded.customer_id,details=excluded.details,
              deleted_at=excluded.deleted_at,revision=aurora_order.revision+1
            WHERE aurora_order.deleted_at IS NOT NULL RETURNING revision
            """:"""
            UPDATE aurora_order SET scheduled_at=@scheduled,customer=@Customer,city=@City,status=@Status,terminal_id=@terminalId,customer_id=@customerId,
              details=CAST(@json AS jsonb),deleted_at=CASE WHEN @deleted THEN now() END,revision=revision+1
            WHERE tenant_id=@tenantId AND source_id=@sourceId AND id=@Id RETURNING revision
            """;
        var revision=await db.QuerySingleAsync<int>(new CommandDefinition(sql,new{tenantId,sourceId,item.Id,scheduled=item.ScheduledAt.ToUniversalTime(),item.Customer,item.City,item.Status,terminalId,customerId,json=normalized.ToJsonString(),deleted},tx,cancellationToken:ct));
        return new($"{sourceId:D}/{item.Id}",revision,normalized,deleted);
    }
    private sealed record Row(long Revision,bool Deleted,string Json);
}
