using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Routing;

public sealed class EquipmentStore(NpgsqlConnectionFactory factory, ITenantContext tenant)
{
    public async Task AddMissing(EquipmentCatalogDto catalog, CancellationToken ct)
    {
        if (catalog.Types is null || catalog.Units is null || catalog.Types.Count > 500 || catalog.Units.Count > 5000)
            throw new FormatException("Import up to 500 equipment types and 5,000 units at a time.");
        if (catalog.Types.Any(x => x is null) || catalog.Units.Any(x => x is null)) throw new FormatException("Imported equipment records cannot be null.");
        foreach (var type in catalog.Types) type.Validate();
        foreach (var unit in catalog.Units) unit.Validate();
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        foreach (var type in catalog.Types)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO routing_equipment_type(tenant_id,code,data) VALUES (@TenantId,@Code,CAST(@Data AS jsonb)) ON CONFLICT DO NOTHING",
                new { tenant.TenantId, type.Code, Data = JsonSerializer.Serialize(type) }, transaction: tx, cancellationToken: ct));
        var known = (await db.QueryAsync<string>(new CommandDefinition("SELECT code FROM routing_equipment_type WHERE tenant_id=@TenantId", new { tenant.TenantId }, transaction: tx, cancellationToken: ct))).ToHashSet();
        if (catalog.Units.Any(x => !known.Contains(x.TypeCode))) throw new FormatException("Every unit must reference a saved or imported type.");
        foreach (var unit in catalog.Units)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data) VALUES (@TenantId,@Id,@TypeCode,CAST(@Data AS jsonb)) ON CONFLICT DO NOTHING",
                new { tenant.TenantId, unit.Id, unit.TypeCode, Data = JsonSerializer.Serialize(unit) }, transaction: tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    public async Task<EquipmentCatalogDto> List(CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        var types = await db.QueryAsync<string>(new CommandDefinition("SELECT data::text FROM routing_equipment_type WHERE tenant_id=@TenantId ORDER BY code", new { tenant.TenantId }, cancellationToken: ct));
        var units = await db.QueryAsync<string>(new CommandDefinition("SELECT data::text FROM routing_equipment_unit WHERE tenant_id=@TenantId ORDER BY id", new { tenant.TenantId }, cancellationToken: ct));
        return new(types.Select(x => JsonSerializer.Deserialize<EquipmentTypeDto>(x)!).ToArray(), units.Select(x => JsonSerializer.Deserialize<EquipmentUnitDto>(x)!).ToArray());
    }

    public async Task Save(EquipmentTypeDto item, CancellationToken ct)
    {
        item.Validate();
        item.Code = item.Code.Trim();
        await using var db = await factory.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO routing_equipment_type(tenant_id,code,data) VALUES (@TenantId,@Code,CAST(@Data AS jsonb))
            ON CONFLICT (tenant_id,code) DO UPDATE SET data=excluded.data, updated_utc=now()
            """, new { tenant.TenantId, item.Code, Data = JsonSerializer.Serialize(item) }, cancellationToken: ct));
    }

    public async Task Save(EquipmentUnitDto item, CancellationToken ct)
    {
        item.Validate();
        item.Id = item.Id.Trim();
        await using var db = await factory.OpenConnectionAsync(ct);
        if(item.UseForRouting)
        {
            var data=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT data::text FROM routing_equipment_type WHERE tenant_id=@TenantId AND code=@TypeCode", new {tenant.TenantId,item.TypeCode},cancellationToken:ct));
            if(data is null || JsonSerializer.Deserialize<EquipmentTypeDto>(data)!.PowerOnly) throw new FormatException("Select a saved truck or powered trailer configuration for route planning.");
        }
        var exists = await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM routing_equipment_type WHERE tenant_id=@TenantId AND code=@TypeCode)", new { tenant.TenantId, item.TypeCode }, cancellationToken: ct));
        if (!exists) throw new FormatException("Save the equipment type before adding its units.");
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data) VALUES (@TenantId,@Id,@TypeCode,CAST(@Data AS jsonb))
            ON CONFLICT (tenant_id,id) DO UPDATE SET type_code=excluded.type_code, data=excluded.data, updated_utc=now()
            """, new { tenant.TenantId, item.Id, item.TypeCode, Data = JsonSerializer.Serialize(item) }, cancellationToken: ct));
    }
}
