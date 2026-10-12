using Aurora.Contracts;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    internal async Task<int[]> ReadyStatuses(NpgsqlConnection db, NpgsqlTransaction? tx, CancellationToken ct)
    {
        RequireEnabled();
        return await db.QuerySingleOrDefaultAsync<int[]>(new CommandDefinition(
            "SELECT ready_statuses FROM aurora_routing_status_configuration WHERE tenant_id=@TenantId AND source_key=@SourceKey",
            new { tenant.TenantId, Options.SourceKey }, tx, cancellationToken: ct)) ?? [Options.ReadyStatus];
    }

    public async Task<NovaRoutingConfiguration> RoutingConfiguration(CancellationToken ct)
    {
        RequireEnabled();
        await using var db = await factory.OpenConnectionAsync(ct);
        var statuses = await ReadyStatuses(db, null, ct);
        var available = (await RecordOptions(ct)).Statuses;
        return new(statuses, available);
    }

    public async Task<int[]> SaveRoutingConfiguration(NovaRoutingConfigurationEdit input, CancellationToken ct, bool rollbackOnly = false)
    {
        RequireEnabled();
        if (input.ReadyStatuses is not { Length: > 0 and <= 200 })
            throw new FormatException("Select at least one Nova status.");
        var selected = input.ReadyStatuses.Distinct().Order().ToArray();
        var available = (await RecordOptions(ct)).Statuses.Select(s => s.Id).ToHashSet();
        if (selected.Any(s => !available.Contains(s)))
            throw new FormatException("Choose valid Nova order statuses.");
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await Lock(db, tx, ct);
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_routing_status_configuration(tenant_id,source_key,ready_statuses)
            VALUES(@TenantId,@SourceKey,@selected)
            ON CONFLICT(tenant_id,source_key) DO UPDATE SET ready_statuses=excluded.ready_statuses
            """, new { tenant.TenantId, Options.SourceKey, selected }, tx, cancellationToken: ct));
        var saved = await ReadyStatuses(db, tx, ct);
        if (rollbackOnly) await tx.RollbackAsync(ct);
        else await tx.CommitAsync(ct);
        return saved;
    }
}
