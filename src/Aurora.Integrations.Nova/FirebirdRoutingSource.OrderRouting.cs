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
        var origin = current.Parts[0].Fields.Single(f => f.Key == "PB_LOCATION").Value;
        if (receiving is not null)
        {
            if (current.Parts.Count(p => p.Title.StartsWith("Consignee")) != 1)
                throw new FormatException("Line haul requires exactly one delivery stop.");
            if (receiving.Equals(origin, StringComparison.OrdinalIgnoreCase)) throw new FormatException("Choose a receiving terminal different from the starting terminal.");
            if (!await fb.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM LOCMAST WHERE LM_ID=@receiving) FROM RDB$DATABASE", new { receiving }, nativeTx, cancellationToken: ct)))
                throw new FormatException("Choose a receiving terminal in this Nova database.");
        }
        var source = SourceId(origin ?? "");
        var orderId = $"FB-{id}";
        if (await pg.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_planning_draft WHERE tenant_id=@TenantId AND source_id=@source
                AND (@orderId=ANY(order_ids) OR @pickupId=ANY(order_ids)) AND finished_session IS NULL AND cancelled_at IS NULL)
            """, new { tenant.TenantId, source, orderId, pickupId=$"FB-P-{id}" }, tx, cancellationToken: ct)))
            throw new FormatException("Cancel the order's open plan before changing its routing.");
        if (await fb.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM OPS_STOP_REC s WHERE s.OPS_ORDER_ID=@id AND s.OPS_TYPE='D'
                AND (COALESCE(s.OPS_MANIFEST_NUMBER,0)<>0 OR s.OPS_DELIVERED_TIME IS NOT NULL
                OR EXISTS(SELECT 1 FROM OPS_MANIFEST_STOPS m WHERE m.OSM_PKUP_DELV_ID=s.OPS_STOP_ID))) FROM RDB$DATABASE
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
