using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    private sealed record OrderRoutingRow(long OrderId, string? ReceivingTerminal, int Revision);

    private async Task<Dictionary<long, OrderRoutingRow>> OrderRouting(long[] ids, CancellationToken ct)
    {
        RequireEnabled();
        await using var db = await factory.OpenConnectionAsync(ct);
        var rows = await db.QueryAsync<OrderRoutingRow>(new CommandDefinition("""
            SELECT order_id AS OrderId, receiving_terminal AS ReceivingTerminal, revision
            FROM aurora_order_routing WHERE tenant_id=@TenantId AND source_key=@SourceKey AND order_id=ANY(@ids)
            """, new { tenant.TenantId, Options.SourceKey, ids }, cancellationToken: ct));
        return rows.ToDictionary(r => r.OrderId);
    }

    public async Task<NovaOrderRouting> ReadOrderRouting(long id, CancellationToken ct)
    {
        await ReadRecord("orders", id, ct);
        var rows = await OrderRouting([id], ct);
        return rows.TryGetValue(id, out var row) ? new(row.ReceivingTerminal, row.Revision) : new(null, 0);
    }

    public async Task<NovaOrderRouting> SaveOrderRouting(long id, NovaOrderRoutingEdit input, CancellationToken ct)
    {
        RequireEnabled();
        var receiving = string.IsNullOrWhiteSpace(input.ReceivingTerminal) ? null : input.ReceivingTerminal.Trim();
        await using var pg = await factory.OpenConnectionAsync(ct);
        await using var tx = await pg.BeginTransactionAsync(ct);
        await Lock(pg, tx, ct);
        await using var fb = await Open(ct);
        await using var nativeTx = fb.BeginTransaction(new FbTransactionOptions {
            TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait,
            WaitTimeout=TimeSpan.FromSeconds(10) });
        var current = await ReadRecord(fb, nativeTx, "orders", id, true, ct);
        if (input.OrderRevision != current.Revision) throw new FormatException("This order changed in Nova. Reopen it before changing routing.");
        var origin = await fb.QuerySingleOrDefaultAsync<string>(new CommandDefinition($"""
            SELECT FIRST 1 {NativeRoutingSql.Terminal()} FROM OPS_HEADER h JOIN OPS_STOP_REC s ON s.OPS_ORDER_ID=h.PB_ID
            WHERE h.PB_ID=@id AND s.OPS_TYPE='D' ORDER BY s.OPS_STOP_NUMBER,s.OPS_STOP_ID
            """,new{id},nativeTx,cancellationToken:ct));
        if (receiving is not null)
        {
            if (current.Parts.Count(p => p.Title.StartsWith("Consignee")) != 1)
                throw new FormatException("Line haul requires exactly one delivery stop.");
            if (receiving.Equals(origin, StringComparison.OrdinalIgnoreCase)) throw new FormatException("Choose a receiving terminal different from the starting terminal.");
            if (!await fb.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM LOCMAST WHERE LM_ID=@receiving) FROM RDB$DATABASE", new { receiving }, nativeTx, cancellationToken: ct)))
                throw new FormatException("Choose a receiving terminal in this Nova database.");
        }
        var orderId = $"FB-{id}";
        if (await pg.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_planning_draft d JOIN aurora_order_source ds
                ON (ds.tenant_id,ds.id)=(d.tenant_id,d.source_id)
                WHERE d.tenant_id=@TenantId AND ds.request#>>'{reporting,firebirdSource}'=@SourceKey
                AND (@orderId=ANY(d.order_ids) OR @pickupId=ANY(d.order_ids)) AND d.finished_session IS NULL AND d.cancelled_at IS NULL)
            """, new { tenant.TenantId, Options.SourceKey, orderId, pickupId=$"FB-P-{id}" }, tx, cancellationToken: ct)))
            throw new FormatException("Cancel the order's open plan before changing its routing.");
        if (await fb.ExecuteScalarAsync<bool>(new CommandDefinition($"""
            SELECT EXISTS(SELECT 1 FROM OPS_STOP_REC s JOIN OPS_HEADER h ON h.PB_ID=s.OPS_ORDER_ID WHERE s.OPS_ORDER_ID=@id AND s.OPS_TYPE='D'
                AND (s.OPS_DELIVERED_TIME IS NOT NULL OR s.OPS_TO_AGENT_TIME IS NOT NULL OR NOT {NativeRoutingSql.Unassigned()})) FROM RDB$DATABASE
            """, new { id }, nativeTx, cancellationToken: ct)))
            throw new FormatException("Routing cannot be changed while the delivery is manifested or completed.");
        var revision = await pg.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
            INSERT INTO aurora_order_routing(tenant_id,source_key,order_id,receiving_terminal,revision)
            SELECT @TenantId,@SourceKey,@id,@receiving,1 WHERE @Revision=0
            ON CONFLICT(tenant_id,source_key,order_id) DO NOTHING RETURNING revision
            """, new { tenant.TenantId, Options.SourceKey, id, receiving, input.Revision }, tx, cancellationToken: ct));
        if (input.Revision > 0)
            revision = await pg.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
                UPDATE aurora_order_routing SET receiving_terminal=@receiving,revision=revision+1
                WHERE tenant_id=@TenantId AND source_key=@SourceKey AND order_id=@id AND revision=@Revision RETURNING revision
                """, new { tenant.TenantId, Options.SourceKey, id, receiving, input.Revision }, tx, cancellationToken: ct));
        if (revision is null) throw new FormatException("Routing changed in another window. Reopen the order before saving.");
        await tx.CommitAsync(ct);
        await nativeTx.RollbackAsync(ct);
        return new(receiving, revision.Value);
    }
}
