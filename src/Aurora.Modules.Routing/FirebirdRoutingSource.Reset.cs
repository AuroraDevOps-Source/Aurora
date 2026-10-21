using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    public static bool IsAuroraManifest(string reference, Guid tenantId) =>
        reference.StartsWith($"aurora:{tenantId:N}:", StringComparison.Ordinal)
        || reference.StartsWith($"aurora-manual:{tenantId:N}:", StringComparison.Ordinal);

    internal async Task<long[]> ResetManifestIds(CancellationToken ct)
    {
        await using var db = await Open(ct);
        return (await db.QueryAsync<long>(new CommandDefinition("""
            SELECT PB_ID FROM OPS_HEADER WHERE PB_TYPE='M'
            AND (PB_EXTERNAL_REF STARTING WITH @routing OR PB_EXTERNAL_REF STARTING WITH @manual)
            ORDER BY PB_ID
            """, new { routing = $"aurora:{tenant.TenantId:N}:", manual = $"aurora-manual:{tenant.TenantId:N}:" }, cancellationToken: ct))).ToArray();
    }

    // One native transaction; the caller keeps Aurora's company lock through its own commit.
    // Retrying after a cross-database commit interruption is safe: absent manifests are already reset.
    internal async Task<int> ResetManifests(long[] ids, int readyStatus, CancellationToken ct, bool rollbackOnly = false)
    {
        await using var db = await Open(ct);
        await using var tx = BeginCreate(db);
        var count = await WriteResetManifests(db, tx, ids, readyStatus, ct);
        if (rollbackOnly) await tx.RollbackAsync(ct); else await tx.CommitAsync(ct);
        return count;
    }

    internal async Task<int> WriteResetManifests(FbConnection db, FbTransaction tx, long[] ids, int readyStatus, CancellationToken ct)
    {
        var orders = new HashSet<long>();
        foreach (var id in ids.Order())
        {
            var reference = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                "SELECT COALESCE(PB_EXTERNAL_REF,'') FROM OPS_HEADER WHERE PB_ID=@id AND PB_TYPE='M' WITH LOCK", new { id }, tx, cancellationToken: ct));
            if (reference is null) continue;
            if (!IsAuroraManifest(reference.Trim(), tenant.TenantId)) throw new FormatException("Reset encountered a manifest that was not created by this company. Nothing was reset in Nova.");
            var members = await DispatchMembers(db, tx, id, true, ct);
            foreach (var member in members.DistinctBy(m => m.Stop))
            {
                // Never change an order also attached to a preserved manifest.
                var shared = await db.ExecuteScalarAsync<int>(new CommandDefinition("""
                    SELECT COUNT(*) FROM OPS_STOP_REC s JOIN OPS_MANIFEST_STOPS l ON l.OSM_PKUP_DELV_ID=s.OPS_STOP_ID
                    JOIN OPS_STOP_REC r ON r.OPS_STOP_ID=l.OSM_STOP_ID
                    WHERE s.OPS_ORDER_ID=@OrderId AND r.OPS_ORDER_ID NOT IN @ids
                    """, new { member.OrderId, ids }, tx, cancellationToken: ct));
                if (shared > 0 || member.ManifestId is > 0 && !ids.Contains(member.ManifestId.Value))
                    throw new FormatException($"Order {member.OrderId} is also assigned to a preserved manifest. Remove that overlap before resetting.");
                await db.ExecuteAsync(new CommandDefinition("""
                    UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=NULL,OPS_MANIFESTED_TIME=NULL,
                        OPS_DRIVER1=NULL,OPS_DRIVER2=NULL,OPS_TRACTOR=NULL,OPS_TRAILER=NULL,
                        OPS_ARRIVE_TIME=NULL,OPS_DEPART_TIME=NULL,OPS_BACKIN_TIME=NULL,
                        OPS_RELEASE_TIME=NULL,OPS_DISPATCH_TIME=NULL,OPS_ONBOARD_TIME=NULL,
                        OPS_DELIVERED_TIME=NULL,OPS_RECOVERED_TIME=NULL,
                        OPS_STATUS=@readyStatus,OPS_STATUS_TIME=CURRENT_TIMESTAMP,
                        OPS_STATUS_REASON='Aurora test reset',OPS_UPDATED_BY='Aurora'
                    WHERE OPS_STOP_ID=@Stop
                    """, new { member.Stop, readyStatus }, tx, cancellationToken: ct));
                orders.Add(member.OrderId);
            }
            await db.ExecuteAsync(new CommandDefinition("UPDATE EQUIP_STATUS SET ES_MANIFEST_NUMBER=NULL,ES_STOP_ID=NULL WHERE ES_MANIFEST_NUMBER=@id", new { id }, tx, cancellationToken: ct));
            // Nova's delete triggers remove routing stops, membership, driver links and manifest detail.
            // Keep audit history; reset does not erase the evidence of previous test activity.
            await db.ExecuteAsync(new CommandDefinition("DELETE FROM OPS_HEADER WHERE PB_ID=@id AND PB_TYPE='M'", new { id }, tx, cancellationToken: ct));
        }
        foreach (var id in orders)
            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE OPS_HEADER SET PB_STATUS=@readyStatus,PB_STATUS_TIME=CURRENT_TIMESTAMP,
                    PB_CURRENT_DRIVER=NULL,PB_CURRENT_TRACTOR=NULL,PB_CURRENT_TRAILER=NULL,
                    PB_PLANNED_DRIVER=NULL,PB_PLANNED_TRACTOR=NULL,PB_PLANNED_TRAILER=NULL
                WHERE PB_ID=@id AND PB_TYPE='O'
                """, new { id, readyStatus }, tx, cancellationToken: ct));
        return orders.Count;
    }
}
