using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;

namespace Aurora.Integrations.Nova.Sync;

// Adopt only snapshots whose recorded source identity matches this connection.
// Other databases' snapshots remain stored, but are not part of this workspace.
public static class NovaLegacyAdoption
{
    public static async Task Apply(NpgsqlConnectionFactory factory,ITenantContext tenant,Guid connection,string sourceKey,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("SELECT id FROM aurora_sync_connection WHERE tenant_id=@TenantId AND id=@connection FOR NO KEY UPDATE",new{tenant.TenantId,connection},tx,cancellationToken:ct));
        var rows=(await db.QueryAsync<LegacyOrder>(new CommandDefinition("""
            SELECT o.source_id::text || '/' || o.id AS LocalId,
              COALESCE(o.details->>'NativeOrderId',substring(o.id from '^FB-(?:P-)?([0-9]+)$')) AS ExternalId
            FROM aurora_order o JOIN aurora_order_source s ON s.tenant_id=o.tenant_id AND s.id=o.source_id
            WHERE o.tenant_id=@TenantId AND s.request#>>'{reporting,firebirdSource}'=@sourceKey
              AND o.deleted_at IS NULL AND NOT EXISTS(SELECT 1 FROM aurora_sync_mapping m
                WHERE m.tenant_id=o.tenant_id AND m.connection_id=@connection AND m.entity_type='order'
                  AND m.local_id=o.source_id::text || '/' || o.id)
            """,new{tenant.TenantId,connection,sourceKey},tx,cancellationToken:ct))).ToArray();
        if(rows.Any(r=>!long.TryParse(r.ExternalId,out _)) || rows.DistinctBy(r=>r.ExternalId).Count()!=rows.Length)
            throw new FormatException("Legacy order snapshots need identity review before synchronization can start.");
        var entities=new OperationalSyncEntities(tenant,new DriverStore(factory,tenant));
        foreach(var row in rows)
        {
            if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_sync_mapping WHERE tenant_id=@TenantId AND connection_id=@connection AND entity_type='order' AND external_id=@ExternalId)",new{tenant.TenantId,connection,row.ExternalId},tx,cancellationToken:ct)))
                throw new FormatException("A legacy order and an imported order share a native identity. Reconcile before continuing.");
            var current=await entities.Read("order",row.LocalId,db,tx,ct)??throw new InvalidOperationException("Legacy order disappeared.");
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO aurora_sync_mapping(tenant_id,connection_id,entity_type,local_id,external_id,external_revision,baseline,external_deleted)
                VALUES(@TenantId,@connection,'order',@LocalId,@ExternalId,'legacy-snapshot',CAST(@baseline AS jsonb),false)
                """,new{tenant.TenantId,connection,row.LocalId,row.ExternalId,baseline=current.Data.ToJsonString()},tx,cancellationToken:ct));
        }
        await tx.CommitAsync(ct);
    }
    private sealed record LegacyOrder(string LocalId,string ExternalId);
}
