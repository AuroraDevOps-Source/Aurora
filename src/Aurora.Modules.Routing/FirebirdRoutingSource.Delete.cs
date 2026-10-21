using Dapper;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    public async Task DeleteTruck(long id, string revision, CancellationToken ct, bool rollbackOnly = false)
    {
        await using var db = await Open(ct);
        await using var tx = BeginCreate(db);
        var current = await ReadRecord(db, tx, "fleet", id, true, ct);
        if (current.Revision != revision) throw new FormatException("This truck changed in Nova. Reload it before deleting.");
        var inUse = await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM OPS_HEADER WHERE PB_CURRENT_TRACTOR=@id OR PB_CURRENT_TRAILER=@id
                OR PB_PLANNED_TRACTOR=@id OR PB_PLANNED_TRAILER=@id)
                OR EXISTS(SELECT 1 FROM OPS_STOP_REC WHERE OPS_TRACTOR=@id OR OPS_TRAILER=@id)
            FROM RDB$DATABASE
            """, new { id }, tx, cancellationToken: ct));
        if (inUse) throw new FormatException("This truck is assigned to orders, manifests or stops and cannot be deleted.");
        await db.ExecuteAsync(new CommandDefinition("UPDATE EQUIP_HEADER SET EH_DELETE_DATE=CURRENT_TIMESTAMP WHERE EH_ID=@id", new { id }, tx, cancellationToken: ct));
        if (rollbackOnly) await tx.RollbackAsync(ct); else await tx.CommitAsync(ct);
    }

    public async Task DeleteTransportationNode(string id, string revision, CancellationToken ct, bool rollbackOnly = false)
    {
        await using var routingDb = await factory.OpenConnectionAsync(ct);
        await using var routingTx = await routingDb.BeginTransactionAsync(ct);
        await Lock(routingDb,routingTx,ct);
        var nodes = await ReadTerminals(ct);
        var terminal = nodes.SingleOrDefault(n=>n.Code==id);
        if (terminal is not null && nodes.Where(n=>n.Id!=terminal.Id).Any(n=>n.OutboundRoutes.Any(r=>r.DestinationTerminalId==terminal.Id || r.ViaTerminalId==terminal.Id)))
            throw new FormatException("This terminal is used by outbound routing rules. Update those rules before deleting it.");
        await using var db = await Open(ct);
        await using var tx = BeginCreate(db);
        var current = await ReadMaster(db, tx, "terminals", id, true, ct);
        if (current.Revision != revision) throw new FormatException("This transportation node changed in Nova. Reload it before deleting.");
        var inUse = await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM EQUIP_HEADER WHERE EH_DOMICILE=@id)
                OR EXISTS(SELECT 1 FROM ACCOUNT WHERE AC_TERM_LOC=@id)
                OR EXISTS(SELECT 1 FROM OPS_HEADER WHERE PB_LOCATION=@id OR PBORIG=@id OR PBDEST=@id
                    OR PBLOCAT=@id OR PB_NEW_ORIG=@id OR PB_STATUS_LOCATION=@id)
                OR EXISTS(SELECT 1 FROM OPS_STOP_REC WHERE OPS_DELIVERY_TERMINAL=@id
                    OR (OPS_MFST_LOC_TYPE='L' AND OPS_MFST_LOCATION=@id)
                    OR (OPS_LOC_TYPE='L' AND OPS_LOCATION=@id))
            FROM RDB$DATABASE
            """, new { id }, tx, cancellationToken: ct));
        if (inUse) throw new FormatException("This transportation node is used by equipment, customers, orders or stops and cannot be deleted.");
        await db.ExecuteAsync(new CommandDefinition("DELETE FROM LOCMAST WHERE LM_ID=@id", new { id }, tx, cancellationToken: ct));
        if (rollbackOnly) await tx.RollbackAsync(ct); else await tx.CommitAsync(ct);
        if (!rollbackOnly)
        {
            await routingDb.ExecuteAsync(new CommandDefinition("DELETE FROM aurora_terminal_routing WHERE tenant_id=@TenantId AND source_key=@SourceKey AND terminal_code=@id",new {tenant.TenantId,Options.SourceKey,id},routingTx,cancellationToken:ct));
            await routingTx.CommitAsync(ct);
        }
    }
}
