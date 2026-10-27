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
        await OperationalOutbox.Lock(db, tx, tenant.TenantId, ct);
        foreach (var type in catalog.Types)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO routing_equipment_type(tenant_id,code,data) VALUES (@TenantId,@Code,CAST(@Data AS jsonb)) ON CONFLICT DO NOTHING",
                new { tenant.TenantId, type.Code, Data = JsonSerializer.Serialize(type) }, transaction: tx, cancellationToken: ct));
        var known = (await db.QueryAsync<string>(new CommandDefinition("SELECT code FROM routing_equipment_type WHERE tenant_id=@TenantId", new { tenant.TenantId }, transaction: tx, cancellationToken: ct))).ToHashSet();
        if (catalog.Units.Any(x => !known.Contains(x.TypeCode))) throw new FormatException("Every unit must reference a saved or imported type.");
        foreach (var unit in catalog.Units)
        {
            var inserted=await db.ExecuteAsync(new CommandDefinition("INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data) VALUES (@TenantId,@Id,@TypeCode,CAST(@Data AS jsonb)) ON CONFLICT DO NOTHING",
                new { tenant.TenantId, unit.Id, unit.TypeCode, Data = JsonSerializer.Serialize(unit) }, transaction: tx, cancellationToken: ct));
            if(inserted==1)await EquipmentSyncEntities.Enqueue(tenant.TenantId,unit.Id,db,tx,ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<EquipmentCatalogDto> List(CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        var types = await db.QueryAsync<string>(new CommandDefinition("SELECT (data || jsonb_build_object('Revision',revision))::text FROM routing_equipment_type WHERE tenant_id=@TenantId ORDER BY code", new { tenant.TenantId }, cancellationToken: ct));
        var units = await db.QueryAsync<string>(new CommandDefinition("SELECT (data || jsonb_build_object('Revision',revision))::text FROM routing_equipment_unit WHERE tenant_id=@TenantId AND deleted_at IS NULL ORDER BY id", new { tenant.TenantId }, cancellationToken: ct));
        return new(types.Select(x => JsonSerializer.Deserialize<EquipmentTypeDto>(x)!).ToArray(), units.Select(x => JsonSerializer.Deserialize<EquipmentUnitDto>(x)!).ToArray());
    }

    public async Task Save(EquipmentTypeDto item, CancellationToken ct)
    {
        item.Validate();
        item.Code = item.Code.Trim();
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await OperationalOutbox.Lock(db, tx, tenant.TenantId, ct);
        var revision = await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
            INSERT INTO routing_equipment_type(tenant_id,code,data)
            SELECT @TenantId,@Code,CAST(@Data AS jsonb) WHERE @Revision=0 OR EXISTS (SELECT 1 FROM routing_equipment_type WHERE tenant_id=@TenantId AND code=@Code)
            ON CONFLICT (tenant_id,code) DO UPDATE SET data=excluded.data, updated_utc=now(),revision=routing_equipment_type.revision+1
            WHERE routing_equipment_type.revision=@Revision
            RETURNING revision
            """, new { tenant.TenantId, item.Code, item.Revision, Data = JsonSerializer.Serialize(item) }, transaction: tx, cancellationToken: ct));
        if (revision is null) throw new FormatException("Equipment changed. Reload it before saving again.");
        var units=await db.QueryAsync<string>(new CommandDefinition("UPDATE routing_equipment_unit SET revision=revision+1,updated_utc=now() WHERE tenant_id=@TenantId AND type_code=@Code AND deleted_at IS NULL RETURNING id",new{tenant.TenantId,item.Code},tx,cancellationToken:ct));
        foreach(var id in units)await EquipmentSyncEntities.Enqueue(tenant.TenantId,id,db,tx,ct);
        await tx.CommitAsync(ct);
        item.Revision = revision.Value;
    }

    public async Task Save(EquipmentUnitDto item, CancellationToken ct)
    {
        item.Validate();
        item.Id = item.Id.Trim();
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await OperationalOutbox.Lock(db, tx, tenant.TenantId, ct);
        if(item.DriverId is {} driverId && !await db.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM aurora_driver WHERE tenant_id=@TenantId AND id=@driverId AND deleted_at IS NULL AND upper(terminal)=upper(@Terminal))",
            new{tenant.TenantId,driverId,item.Terminal},tx,cancellationToken:ct)))
            throw new FormatException("Choose a driver from this truck's terminal.");
        if(item.UseForRouting)
        {
            var data=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT data::text FROM routing_equipment_type WHERE tenant_id=@TenantId AND code=@TypeCode", new {tenant.TenantId,item.TypeCode},transaction:tx,cancellationToken:ct));
            if(data is null || JsonSerializer.Deserialize<EquipmentTypeDto>(data)!.PowerOnly) throw new FormatException("Select a saved truck or powered trailer configuration for route planning.");
        }
        var exists = await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM routing_equipment_type WHERE tenant_id=@TenantId AND code=@TypeCode)", new { tenant.TenantId, item.TypeCode }, transaction:tx, cancellationToken: ct));
        if (!exists) throw new FormatException("Save the equipment type before adding its units.");
        var revision = await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
            INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data)
            SELECT @TenantId,@Id,@TypeCode,CAST(@Data AS jsonb) WHERE @Revision=0 OR EXISTS (SELECT 1 FROM routing_equipment_unit WHERE tenant_id=@TenantId AND id=@Id)
            ON CONFLICT (tenant_id,id) DO UPDATE SET type_code=excluded.type_code, data=excluded.data, updated_utc=now(),revision=routing_equipment_unit.revision+1
            WHERE routing_equipment_unit.revision=@Revision AND routing_equipment_unit.deleted_at IS NULL
            RETURNING revision
            """, new { tenant.TenantId, item.Id, item.TypeCode, item.Revision, Data = JsonSerializer.Serialize(item) }, transaction:tx, cancellationToken: ct));
        if (revision is null) throw new FormatException("Equipment changed. Reload it before saving again.");
        await EquipmentSyncEntities.Enqueue(tenant.TenantId,item.Id,db,tx,ct);
        await tx.CommitAsync(ct);
        item.Revision = revision.Value;
    }
    public async Task Delete(string id,long revision,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await OperationalOutbox.Lock(db,tx,tenant.TenantId,ct);
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_manifest WHERE tenant_id=@TenantId AND vehicle=@id AND deleted_at IS NULL AND status<>'Complete')",new{tenant.TenantId,id},tx,cancellationToken:ct)))
            throw new FormatException("This equipment is assigned to an active manifest.");
        if(await db.ExecuteAsync(new CommandDefinition("UPDATE routing_equipment_unit SET deleted_at=now(),revision=revision+1,updated_utc=now() WHERE tenant_id=@TenantId AND id=@id AND revision=@revision AND deleted_at IS NULL",new{tenant.TenantId,id,revision},tx,cancellationToken:ct))!=1)
            throw new FormatException("Equipment changed. Reload it before deleting.");
        await EquipmentSyncEntities.Enqueue(tenant.TenantId,id,db,tx,ct);
        await tx.CommitAsync(ct);
    }
}
