using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

internal static class EquipmentSyncEntities
{
    internal const string Select = """
        SELECT u.revision AS Revision,u.deleted_at IS NOT NULL AS Deleted,
          jsonb_build_object('Unit',(u.data || jsonb_build_object('Id',u.id)) - 'Revision' - 'TypeCode',
            'Type',t.data - 'Revision' - 'Code')::text AS Json
        FROM routing_equipment_unit u JOIN routing_equipment_type t ON (t.tenant_id,t.code)=(u.tenant_id,u.type_code)
        WHERE u.tenant_id=@tenantId AND u.id=@id
        """;
    public static async Task<SyncEntity?> Read(Guid tenantId,string id,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(Select+" FOR UPDATE OF u,t",new{tenantId,id},tx,cancellationToken:ct));
        return row is null?null:new(id,row.Revision,JsonNode.Parse(row.Json)!.AsObject(),row.Deleted);
    }
    public static async Task Enqueue(Guid tenantId,string id,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var row=await Read(tenantId,id,db,tx,ct)??throw new KeyNotFoundException("Equipment not found.");
        await SyncStore.Enqueue(db,tx,tenantId,"equipment",id,row.Revision,row.Deleted?"delete":"upsert",row.Data.ToJsonString(),ct);
    }
    public static async Task<SyncEntity> Write(Guid tenantId,string? localId,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var unit=data["Unit"]?.Deserialize<EquipmentUnitDto>()??throw new FormatException("Incoming equipment is missing its unit.");
        var type=data["Type"]?.Deserialize<EquipmentTypeDto>()??throw new FormatException("Incoming equipment is missing its routing properties.");
        if(localId is not null&&localId!=unit.Id)throw new FormatException("Equipment identity cannot change.");
        // Imported units own their properties; a capacity change must not mutate another unit sharing a legacy type.
        type.Code="unit:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(unit.Id)));
        unit.TypeCode=type.Code;type.Validate();unit.Validate();
        if(unit.UseForRouting&&type.PowerOnly)throw new FormatException("Choose a complete powered equipment configuration.");
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO routing_equipment_type(tenant_id,code,data) VALUES(@tenantId,@Code,CAST(@json AS jsonb))
            ON CONFLICT(tenant_id,code) DO UPDATE SET data=excluded.data,revision=routing_equipment_type.revision+1,updated_utc=now()
            """,new{tenantId,type.Code,json=JsonSerializer.Serialize(type)},tx,cancellationToken:ct));
        var sql=localId is null?"""
            INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data,deleted_at)
            VALUES(@tenantId,@Id,@TypeCode,CAST(@json AS jsonb),CASE WHEN @deleted THEN now() END) RETURNING revision
            """:"""
            UPDATE routing_equipment_unit SET type_code=@TypeCode,data=CAST(@json AS jsonb),deleted_at=CASE WHEN @deleted THEN now() END,
              revision=revision+1,updated_utc=now() WHERE tenant_id=@tenantId AND id=@Id RETURNING revision
            """;
        var revision=await db.QuerySingleAsync<long>(new CommandDefinition(sql,new{tenantId,unit.Id,unit.TypeCode,json=JsonSerializer.Serialize(unit),deleted},tx,cancellationToken:ct));
        return new(unit.Id,revision,(JsonObject)data.DeepClone(),deleted);
    }
    private sealed record Row(long Revision,bool Deleted,string Json);
}
