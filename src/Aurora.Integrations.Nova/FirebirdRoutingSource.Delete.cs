using Dapper;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

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
        await using var db = await Open(ct);
        await using var tx = BeginCreate(db);
        await DeleteTerminalRecord(db,tx,id,revision,ct);
        if (rollbackOnly) await tx.RollbackAsync(ct); else await tx.CommitAsync(ct);
        if (!rollbackOnly)
        {
            await routingDb.ExecuteAsync(new CommandDefinition("DELETE FROM aurora_terminal_routing WHERE tenant_id=@TenantId AND source_key=@SourceKey AND terminal_code=@id",new {tenant.TenantId,Options.SourceKey,id},routingTx,cancellationToken:ct));
            await routingTx.CommitAsync(ct);
        }
    }
    internal static async Task DeleteTerminalRecord(FirebirdSql.Data.FirebirdClient.FbConnection db,
        FirebirdSql.Data.FirebirdClient.FbTransaction tx,string id,string revision,CancellationToken ct)
    {
        var current=await ReadMaster(db,tx,"terminals",id,true,ct);
        if(current.Revision!=revision)throw new FormatException("This terminal changed in Nova. Reload it before deleting.");
        // Terminal deletion is allowed even when its code is referenced. Keep those business
        // records and their existing terminal codes; do not cascade-delete or reassign them.
        await db.ExecuteAsync(new CommandDefinition("DELETE FROM LOCMAST WHERE LM_ID=@id",new{id},tx,cancellationToken:ct));
    }

}
