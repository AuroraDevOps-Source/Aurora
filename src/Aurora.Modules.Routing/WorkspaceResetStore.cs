using System.Security.Cryptography;
using System.Text;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

public sealed class WorkspaceResetStore(NpgsqlConnectionFactory factory, ITenantContext tenant, IExternalWorkspaceReset source)
{
    public async Task<WorkspaceResetPreview> Preview(CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await Lock(db, tx, ct);
        return (await Snapshot(db, tx, ct)).Preview;
    }

    public async Task<(WorkspaceResetResult Result, Guid[] Drafts)> Execute(WorkspaceResetCommand input, CancellationToken ct)
    {
        if (input.Confirmation != "RESET") throw new FormatException("Type RESET to confirm the testing reset.");
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await Lock(db, tx, ct);
        await db.ExecuteAsync(new CommandDefinition("LOCK TABLE aurora_manifest_order,aurora_manifest,aurora_planning_draft,aurora_order,aurora_freight_workflow,route_plan IN SHARE ROW EXCLUSIVE MODE", transaction: tx, cancellationToken: ct));
        var snapshot = await Snapshot(db, tx, ct);
        if (input.Revision != snapshot.Preview.Revision) throw new FormatException("The test data changed. Review the reset counts again.");
        var localOrders = await db.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_order o SET status=@ready,revision=revision+1
            WHERE o.tenant_id=@TenantId AND EXISTS(SELECT 1 FROM aurora_manifest_order m
                WHERE m.tenant_id=o.tenant_id AND m.source_id=o.source_id AND m.order_id=o.id);
            """, new { tenant.TenantId, ready = WorkspaceOrderStatus.Ready }, tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_freight_workflow SET data='{}',revision=revision+1,updated_at=now() WHERE tenant_id=@TenantId;
            DELETE FROM aurora_manifest_order WHERE tenant_id=@TenantId;
            DELETE FROM aurora_manifest WHERE tenant_id=@TenantId;
            DELETE FROM aurora_planning_draft WHERE tenant_id=@TenantId;
            DELETE FROM route_plan WHERE tenant_id=@TenantId;
            """, new { tenant.TenantId }, tx, cancellationToken: ct));
        var nativeOrders = 0;
        if (snapshot.NativeIds.Length > 0)
        {
            var settings = await source.RoutingConfiguration(ct);
            nativeOrders = await source.ResetManifests(snapshot.NativeIds, settings.ReadyStatuses[0], ct);
        }
        // If the local commit fails after Nova commits, retrying safely finishes the local cleanup.
        await tx.CommitAsync(ct);
        return (new(snapshot.Preview.Manifests, snapshot.Preview.Drafts, Math.Max(localOrders, nativeOrders)), snapshot.DraftIds);
    }

    private Task Lock(NpgsqlConnection db, NpgsqlTransaction tx, CancellationToken ct) => db.ExecuteAsync(new CommandDefinition(
        "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", new { key = $"firebird-routing:{tenant.TenantId}" }, tx, cancellationToken: ct));

    private async Task<(WorkspaceResetPreview Preview, long[] NativeIds, Guid[] DraftIds)> Snapshot(NpgsqlConnection db, NpgsqlTransaction tx, CancellationToken ct)
    {
        var native = source.Enabled ? await source.ResetManifestIds(ct) : [];
        var manifests = (await db.QueryAsync<LocalManifest>(new CommandDefinition("SELECT id,number FROM aurora_manifest WHERE tenant_id=@TenantId ORDER BY id", new { tenant.TenantId }, tx, cancellationToken: ct))).ToArray();
        var drafts = (await db.QueryAsync<Guid>(new CommandDefinition("SELECT id FROM aurora_planning_draft WHERE tenant_id=@TenantId ORDER BY id", new { tenant.TenantId }, tx, cancellationToken: ct))).ToArray();
        var orders = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM aurora_order WHERE tenant_id=@TenantId AND deleted_at IS NULL", new { tenant.TenantId }, tx, cancellationToken: ct));
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { tenant.TenantId, native, manifests, drafts, orders }))));
        var count = native.Length + manifests.Count(m => !long.TryParse(m.Number, out var id) || !native.Contains(id));
        return (new(revision, count, drafts.Length, orders), native, drafts);
    }
    private sealed record LocalManifest(Guid Id, string? Number);
}
