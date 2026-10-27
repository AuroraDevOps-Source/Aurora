using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

internal static class ManifestSyncEntities
{
    internal const string Select = """
        SELECT revision::bigint AS Revision,deleted_at IS NOT NULL AS Deleted,
          jsonb_build_object('Number',number,'ManifestDate',manifest_date,'Status',status,'Vehicle',vehicle,'Details',details,'Route',data,
            'Orders',COALESCE((SELECT jsonb_agg(jsonb_build_object('SourceId',a.source_id,'Id',a.order_id,'StopNumber',a.stop_number) ORDER BY a.stop_number,a.order_id)
            FROM aurora_manifest_order a WHERE a.tenant_id=m.tenant_id AND a.manifest_id=m.id),'[]'::jsonb))::text AS Json
        FROM aurora_manifest m WHERE tenant_id=@tenantId AND id=@id
        """;
    public static async Task<SyncEntity?> Read(Guid tenantId,string localId,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var id=Guid.Parse(localId);
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(Select+" FOR UPDATE OF m",new{tenantId,id},tx,cancellationToken:ct));
        return row is null?null:new(localId,row.Revision,JsonNode.Parse(row.Json)!.AsObject(),row.Deleted);
    }
    public static async Task<SyncEntity> Write(Guid tenantId,string? localId,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var item=data.Deserialize<TmsManifest>()??throw new FormatException("Invalid incoming manifest.");item.Validate();
        var id=localId is null?Guid.NewGuid():Guid.Parse(localId);
        var orders=data["Orders"]?.Deserialize<Assignment[]>()??throw new FormatException("Incoming manifest is missing its orders.");
        if(orders.Length>5000 || orders.Any(o=>o.SourceId==Guid.Empty||string.IsNullOrWhiteSpace(o.Id)||o.StopNumber<1) || orders.DistinctBy(o=>(o.SourceId,o.Id)).Count()!=orders.Length)
            throw new FormatException("Incoming manifest has invalid or duplicate orders.");
        var previous=(await db.QueryAsync<Assignment>(new CommandDefinition("SELECT source_id AS SourceId,order_id AS Id,stop_number AS StopNumber FROM aurora_manifest_order WHERE tenant_id=@tenantId AND manifest_id=@id",new{tenantId,id},tx,cancellationToken:ct))).ToArray();
        foreach(var order in deleted?Array.Empty<Assignment>():orders)
        {
            if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_order WHERE tenant_id=@tenantId AND source_id=@SourceId AND id=@Id AND deleted_at IS NULL)",new{tenantId,order.SourceId,order.Id},tx,cancellationToken:ct)))
                throw new FormatException("Import the manifest's orders before its membership.");
            if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_manifest_order WHERE tenant_id=@tenantId AND source_id=@SourceId AND order_id=@Id AND manifest_id<>@manifestId)",new{tenantId,order.SourceId,order.Id,manifestId=id},tx,cancellationToken:ct)))
                throw new FormatException("An incoming manifest order is assigned to another manifest.");
        }
        foreach(var order in previous.Concat(deleted?Array.Empty<Assignment>():orders).DistinctBy(o=>(o.SourceId,o.Id)))
        {
            var orderKey=$"{order.SourceId:D}/{order.Id}";
            if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_sync_outbox WHERE tenant_id=@tenantId AND entity_type='order' AND local_id=@orderKey AND state IN ('pending','working','conflict'))",new{tenantId,orderKey},tx,cancellationToken:ct)))
                throw new SyncBusyException();
        }
        var sql=localId is null?"""
            INSERT INTO aurora_manifest(tenant_id,id,number,manifest_date,status,vehicle,details,data,deleted_at)
            VALUES(@tenantId,@id,@Number,@date,@Status,@Vehicle,CAST(@details AS jsonb),CAST(@route AS jsonb),CASE WHEN @deleted THEN now() END) RETURNING revision
            """:"""
            UPDATE aurora_manifest SET number=@Number,manifest_date=@date,status=@Status,vehicle=@Vehicle,details=CAST(@details AS jsonb),data=CAST(@route AS jsonb),
              deleted_at=CASE WHEN @deleted THEN now() END,revision=revision+1 WHERE tenant_id=@tenantId AND id=@id RETURNING revision
            """;
        var revision=await db.QuerySingleAsync<int>(new CommandDefinition(sql,new{tenantId,id,item.Number,date=item.ManifestDate.ToDateTime(TimeOnly.MinValue),item.Status,item.Vehicle,
            details=data["Details"]!.ToJsonString(),route=data["Route"]?.ToJsonString()??"{}",deleted},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("DELETE FROM aurora_manifest_order WHERE tenant_id=@tenantId AND manifest_id=@id",new{tenantId,id},tx,cancellationToken:ct));
        foreach(var order in deleted?Array.Empty<Assignment>():orders)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest_order(tenant_id,manifest_id,source_id,order_id,stop_number) VALUES(@tenantId,@manifestId,@SourceId,@Id,@StopNumber)",new{tenantId,manifestId=id,order.SourceId,order.Id,order.StopNumber},tx,cancellationToken:ct));
        // Membership and order execution are separate aggregates. The source's
        // order snapshot supplies status; importing a manifest must not fabricate
        // delivery events or mutate a mapped order behind its sync baseline.
        return new(id.ToString(),revision,(JsonObject)data.DeepClone(),deleted);
    }
    private sealed record Row(long Revision,bool Deleted,string Json);
    private sealed record Assignment(Guid SourceId,string Id,int StopNumber);
}
