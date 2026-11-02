using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;

namespace Aurora.Modules.Routing;

public sealed class FreightOpsManifestPublishing(NpgsqlConnectionFactory factory,ITenantContext tenant)
{
    // A submitted plan may already exist remotely even when its reply was lost.
    // MVP handoff freezes local edits instead of silently diverging membership/status.
    internal static async Task RequireLocalPlan(Guid tenantId,Guid id,Npgsql.NpgsqlConnection db,Npgsql.NpgsqlTransaction tx,CancellationToken ct)
    {
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_sync_outbox o JOIN aurora_sync_connection c ON (c.tenant_id,c.id)=(o.tenant_id,o.connection_id)
            WHERE o.tenant_id=@tenantId AND c.provider='freightops' AND o.entity_type='manifest' AND o.local_id=@localId)
            """,new{tenantId,localId=id.ToString()},tx,cancellationToken:ct)))throw new FormatException("This plan was submitted to FreightOps. Manage its assignments and execution there; local editing and reoptimization are locked for this MVP.");
    }
    public async Task Publish(Guid id,int revision,Guid connectionId,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await OperationalOutbox.Lock(db,tx,tenant.TenantId,ct);
        var item=await ManifestSyncEntities.Read(tenant.TenantId,id.ToString(),db,tx,ct)??throw new KeyNotFoundException("Manifest not found.");
        if(item.Deleted||item.Revision!=revision)throw new FormatException("Manifest changed. Refresh before publishing.");
        if(item.Data["Status"]?.ToString()!="Planning")throw new FormatException("Only planning manifests can be published.");
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT NOT EXISTS(SELECT 1 FROM aurora_manifest_order WHERE tenant_id=@TenantId AND manifest_id=@id)
              OR EXISTS(SELECT 1 FROM aurora_manifest_order a JOIN aurora_order o ON (o.tenant_id,o.source_id,o.id)=(a.tenant_id,a.source_id,a.order_id)
                WHERE a.tenant_id=@TenantId AND a.manifest_id=@id AND (NOT aurora_source_in_workspace(a.tenant_id,a.source_id) OR o.deleted_at IS NOT NULL OR COALESCE(o.details->>'WorkType','Delivery')<>'Delivery'))
            """,new{tenant.TenantId,id},tx,cancellationToken:ct)))throw new FormatException("Publish requires current-workspace delivery orders. Pickup and mixed routes need a separate workflow.");
        await SyncStore.Enqueue(db,tx,tenant.TenantId,"manifest",id.ToString(),revision,"upsert",item.Data.ToJsonString(),ct,onlyConnectionId:connectionId);
        await tx.CommitAsync(ct);
    }
}
