using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

public sealed class FreightWorkflowStore(NpgsqlConnectionFactory factory, ITenantContext tenant)
{
    sealed record Row(string Data, int Revision);
    public async Task<FreightWorkflow> Get(Guid source, string id, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        if (!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@source AND id=@id AND deleted_at IS NULL)", new { tenant.TenantId, source, id }, cancellationToken: ct)))
            throw new KeyNotFoundException("Order not found.");
        return await Read(db, null, source, id, ct);
    }

    async Task<FreightWorkflow> Read(NpgsqlConnection db, NpgsqlTransaction? tx, Guid source, string id, CancellationToken ct)
    {
        var row = await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT data::text AS Data, revision AS Revision FROM aurora_freight_workflow WHERE tenant_id=@TenantId AND source_id=@source AND order_id=@id", new { tenant.TenantId, source, id }, tx, cancellationToken: ct));
        if (row is null) return new();
        var workflow = JsonSerializer.Deserialize<FreightWorkflow>(row.Data)!;
        workflow.Revision = row.Revision;
        return workflow;
    }

    // The order lock serializes initial creation, plan edits, and execution with deletion.
    async Task LockOrder(NpgsqlConnection db, NpgsqlTransaction tx, Guid source, string id, CancellationToken ct)
    {
        var found = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT id FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@source AND id=@id AND deleted_at IS NULL FOR UPDATE", new { tenant.TenantId, source, id }, tx, cancellationToken: ct));
        if (found is null) throw new KeyNotFoundException("Order not found.");
    }

    public async Task<FreightWorkflow> Save(Guid source, string id, FreightWorkflow input, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await LockOrder(db, tx, source, id, ct);
        var current = await Read(db, tx, source, id, ct);
        if (input.Revision != current.Revision) throw new FreightConflictException();
        input.ValidateReplacement(current);
        // Node references must belong to this tenant. Historical snapshots on frozen activities remain valid.
        var frozenIds = current.Activities.Where(a => current.Events.Any(e => e.ActivityId == a.Id)).Select(a => a.Id).ToHashSet();
        foreach (var nodeId in input.Activities.Where(a => !frozenIds.Contains(a.Id)).SelectMany(a => new[] { a.Origin.NodeId, a.Destination?.NodeId }).OfType<Guid>().Distinct())
            if (!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_terminal WHERE tenant_id=@TenantId AND id=@nodeId AND deleted_at IS NULL)", new { tenant.TenantId, nodeId }, tx, cancellationToken: ct)))
                throw new FormatException("Choose a saved terminal from your company.");
        await Write(db, tx, source, id, input, ct);
        await tx.CommitAsync(ct);
        return input;
    }

    public async Task<FreightWorkflow> Execute(Guid source, string id, FreightExecution command, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await LockOrder(db, tx, source, id, ct);
        var workflow = await Read(db, tx, source, id, ct);
        if (workflow.Execute(command, tenant.UserId, DateTimeOffset.UtcNow)) await Write(db, tx, source, id, workflow, ct);
        await tx.CommitAsync(ct);
        return workflow;
    }

    async Task Write(NpgsqlConnection db, NpgsqlTransaction tx, Guid source, string id, FreightWorkflow workflow, CancellationToken ct)
    {
        workflow.Revision++;
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_freight_workflow(tenant_id,source_id,order_id,revision,data)
            VALUES(@TenantId,@source,@id,@Revision,CAST(@json AS jsonb))
            ON CONFLICT(tenant_id,source_id,order_id) DO UPDATE
            SET revision=excluded.revision,data=excluded.data,updated_at=now()
            """, new { tenant.TenantId, source, id, workflow.Revision, json = JsonSerializer.Serialize(workflow) }, tx, cancellationToken: ct));
    }
}
