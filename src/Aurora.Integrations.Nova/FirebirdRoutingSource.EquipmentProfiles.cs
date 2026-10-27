using System.Text.Json;
using Aurora.Contracts;
using Dapper;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    // Use the existing tenant-isolated equipment settings store. Source and native ID
    // prevent settings from leaking across connected Nova databases or renamed units.
    private string EquipmentProfileKey(long id) => $"nova-profile:{Options.SourceKey}:{id}";

    public async Task<EquipmentRoutingProfile> ReadEquipmentProfile(long id, CancellationToken ct)
    {
        await ReadRecord("fleet", id, ct);
        await using var pg = await factory.OpenConnectionAsync(ct);
        var json = await pg.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT data::text FROM routing_equipment_type WHERE tenant_id=@TenantId AND code=@code",
            new { tenant.TenantId, code=EquipmentProfileKey(id) }, cancellationToken:ct));
        return json is null ? new() : JsonSerializer.Deserialize<EquipmentRoutingProfile>(json)!;
    }

    public async Task<EquipmentRoutingProfile> SaveEquipmentProfile(long id, EquipmentRoutingProfileEdit input, CancellationToken ct)
    {
        EquipmentRoutingProfiles.Validate(input.Profile);
        await using var pg = await factory.OpenConnectionAsync(ct);
        await using var tx = await pg.BeginTransactionAsync(ct);
        await Lock(pg, tx, ct);
        await using var fb = await Open(ct);
        await using var nativeTx = BeginCreate(fb);
        var record = await ReadRecord(fb, nativeTx, "fleet", id, true, ct);
        if(record.Revision != input.EquipmentRevision) throw new FormatException("Equipment changed. Reopen it before saving its routing profile.");
        var code = EquipmentProfileKey(id);
        var json = await pg.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT data::text FROM routing_equipment_type WHERE tenant_id=@TenantId AND code=@code",
            new { tenant.TenantId, code }, tx, cancellationToken:ct));
        var current = json is null ? new EquipmentRoutingProfile() : JsonSerializer.Deserialize<EquipmentRoutingProfile>(json)!;
        if(current.Revision != input.Revision) throw new FormatException("Routing profile changed. Reopen the equipment before saving.");
        var saved = new EquipmentRoutingProfile(input.Profile, current.Revision+1);
        await pg.ExecuteAsync(new CommandDefinition("""
            INSERT INTO routing_equipment_type(tenant_id,code,data) VALUES(@TenantId,@code,CAST(@data AS jsonb))
            ON CONFLICT(tenant_id,code) DO UPDATE SET data=excluded.data,updated_utc=now()
            """, new { tenant.TenantId, code, data=JsonSerializer.Serialize(saved) }, tx, cancellationToken:ct));
        await tx.CommitAsync(ct);
        return saved;
    }

    private async Task ApplyEquipmentProfiles(EquipmentCatalogDto catalog, CancellationToken ct)
    {
        await using var pg = await factory.OpenConnectionAsync(ct);
        var keys = catalog.Types.ToDictionary(t=>EquipmentProfileKey(long.Parse(t.Code[5..],System.Globalization.CultureInfo.InvariantCulture)));
        if(keys.Count==0) return;
        var rows = await pg.QueryAsync<EquipmentProfileRow>(new CommandDefinition(
            "SELECT code,data::text AS Json FROM routing_equipment_type WHERE tenant_id=@TenantId AND code=ANY(@codes)",
            new { tenant.TenantId, codes=keys.Keys.ToArray() }, cancellationToken:ct));
        foreach(var row in rows)
        {
            var setting = JsonSerializer.Deserialize<EquipmentRoutingProfile>(row.Json)!;
            EquipmentRoutingProfiles.Validate(setting.Profile);
            var type = keys[row.Code];
            type.RoutingProfile = setting.Profile;
            // Explicitly configured equipment represents the complete cargo-carrying rig.
            // Capacity and costs must still be configured before it is planning-ready.
            type.PowerOnly = false;
        }
    }
    private sealed record EquipmentProfileRow(string Code, string Json);
}
