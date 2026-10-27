using Aurora.Contracts;
using Dapper;
using Npgsql;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    internal async Task<int[]> ReadyStatuses(NpgsqlConnection db, NpgsqlTransaction? tx, CancellationToken ct)
    {
        RequireEnabled();
        return await db.QuerySingleOrDefaultAsync<int[]>(new CommandDefinition(
            "SELECT ready_statuses FROM aurora_routing_status_configuration WHERE tenant_id=@TenantId AND source_key=@SourceKey",
            new { tenant.TenantId, Options.SourceKey }, tx, cancellationToken: ct)) ?? [Options.ReadyStatus];
    }

    internal async Task<int[]> PickupReadyStatuses(NpgsqlConnection db,NpgsqlTransaction? tx,CancellationToken ct) =>
        await db.QuerySingleOrDefaultAsync<int[]>(new CommandDefinition("SELECT pickup_ready_statuses FROM aurora_routing_status_configuration WHERE tenant_id=@TenantId AND source_key=@SourceKey",new{tenant.TenantId,Options.SourceKey},tx,cancellationToken:ct)) ?? [];

    public async Task<NovaRoutingConfiguration> RoutingConfiguration(CancellationToken ct)
    {
        RequireEnabled();
        await using var db = await factory.OpenConnectionAsync(ct);
        var statuses = await ReadyStatuses(db, null, ct);
        var available = (await RecordOptions(ct)).Statuses;
        return new(statuses, available) { PickupReadyStatuses=await PickupReadyStatuses(db,null,ct) };
    }

    public async Task<int[]> SaveRoutingConfiguration(NovaRoutingConfigurationEdit input, CancellationToken ct, bool rollbackOnly = false)
    {
        RequireEnabled();
        if (input.ReadyStatuses is not { Length: > 0 and <= 200 })
            throw new FormatException("Select at least one Nova status.");
        var selected = input.ReadyStatuses.Distinct().Order().ToArray();
        var pickups = (input.PickupReadyStatuses ?? throw new FormatException("Provide pickup statuses, or an empty list to disable pickups.")).Distinct().Order().ToArray();
        if(pickups.Length>200 || selected.Intersect(pickups).Any())throw new FormatException("Use different statuses for pickups and deliveries so each order has one planning task.");
        var available = (await RecordOptions(ct)).Statuses.Select(s => s.Id).ToHashSet();
        if (selected.Concat(pickups).Any(s => !available.Contains(s)))
            throw new FormatException("Choose valid Nova order statuses.");
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await Lock(db, tx, ct);
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_routing_status_configuration(tenant_id,source_key,ready_statuses,pickup_ready_statuses)
            VALUES(@TenantId,@SourceKey,@selected,@pickups)
            ON CONFLICT(tenant_id,source_key) DO UPDATE SET ready_statuses=excluded.ready_statuses,pickup_ready_statuses=excluded.pickup_ready_statuses
            """, new { tenant.TenantId, Options.SourceKey, selected, pickups }, tx, cancellationToken: ct));
        var saved = await ReadyStatuses(db, tx, ct);
        if (rollbackOnly) await tx.RollbackAsync(ct);
        else await tx.CommitAsync(ct);
        return saved;
    }
}
