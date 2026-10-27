using System.Text.Json.Nodes;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Sync;

public sealed record SyncStatus(bool Enabled,long Pending,long Working,long Conflicts,DateTime? LastSuccess);
public sealed record SyncConflict(Guid Id,string EntityType,string LocalId,string ExternalId,string LocalValue,string ExternalValue,string Fields,long LocalRevision,bool LocalDeleted,bool ExternalDeleted);
public sealed record SyncResolution(long LocalRevision,JsonObject Data,bool Deleted);

public sealed class SyncReview(NpgsqlConnectionFactory factory,ITenantContext tenant,ISyncEntityStore entities)
{
    public async Task<SyncStatus?> Status(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<SyncStatus>(new CommandDefinition("""
            SELECT c.enabled,(SELECT count(*) FROM aurora_sync_outbox o WHERE o.tenant_id=c.tenant_id AND o.connection_id=c.id AND o.state='pending') AS Pending,
              (SELECT count(*) FROM aurora_sync_outbox o WHERE o.tenant_id=c.tenant_id AND o.connection_id=c.id AND o.state='working') AS Working,
              (SELECT count(*) FROM (SELECT entity_type,local_id FROM aurora_sync_conflict f WHERE f.tenant_id=c.tenant_id AND f.connection_id=c.id AND f.resolved_at IS NULL
                  UNION SELECT entity_type,local_id FROM aurora_sync_outbox o WHERE o.tenant_id=c.tenant_id AND o.connection_id=c.id AND o.state='conflict') conflicts) AS Conflicts,
              c.last_success_at AS LastSuccess FROM aurora_sync_connection c WHERE c.tenant_id=@TenantId
            """,new{tenant.TenantId},cancellationToken:ct));
    }

    public async Task<SyncConflict[]> Conflicts(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return (await db.QueryAsync<SyncConflict>(new CommandDefinition("""
            SELECT DISTINCT ON(entity_type,local_id) id,entity_type AS EntityType,local_id AS LocalId,external_id AS ExternalId,
              local_value::text AS LocalValue,external_value::text AS ExternalValue,fields::text AS Fields,
              local_revision AS LocalRevision,local_deleted AS LocalDeleted,external_deleted AS ExternalDeleted
            FROM aurora_sync_conflict WHERE tenant_id=@TenantId AND resolved_at IS NULL
            ORDER BY entity_type,local_id,sequence DESC
            """,new{tenant.TenantId},cancellationToken:ct))).ToArray();
    }

    public async Task Resolve(Guid id,SyncResolution resolution,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        var connectionId=await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT id FROM aurora_sync_connection WHERE tenant_id=@TenantId FOR NO KEY UPDATE",new{tenant.TenantId},tx,cancellationToken:ct))??throw new KeyNotFoundException("Integration connection not found.");
        var conflict=await db.QuerySingleOrDefaultAsync<ConflictRow>(new CommandDefinition("""
            SELECT entity_type AS EntityType,local_id AS LocalId,external_value::text AS ExternalValue,
              external_revision AS ExternalRevision,external_deleted AS ExternalDeleted,sequence
            FROM aurora_sync_conflict WHERE tenant_id=@TenantId AND id=@id AND resolved_at IS NULL FOR UPDATE
            """,new{tenant.TenantId,id},tx,cancellationToken:ct))??throw new KeyNotFoundException("Conflict was already resolved or no longer exists.");
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_sync_conflict WHERE tenant_id=@TenantId AND entity_type=@EntityType AND local_id=@LocalId AND sequence>@Sequence AND resolved_at IS NULL)",new{tenant.TenantId,conflict.EntityType,conflict.LocalId,conflict.Sequence},tx,cancellationToken:ct)))throw new FormatException("A newer external change arrived. Reload the conflict before resolving it.");
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_sync_outbox WHERE tenant_id=@TenantId AND entity_type=@EntityType AND local_id=@LocalId AND state='working')",new{tenant.TenantId,conflict.EntityType,conflict.LocalId},tx,cancellationToken:ct)))throw new SyncBusyException();
        var current=await entities.Read(conflict.EntityType,conflict.LocalId,db,tx,ct)??throw new KeyNotFoundException("Local record not found.");
        if(current.Revision!=resolution.LocalRevision)throw new FormatException("The local record changed. Reload before resolving this conflict.");
        var saved=await entities.Write(conflict.EntityType,current.Id,resolution.Data,resolution.Deleted,db,tx,ct);
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_sync_mapping SET baseline=CAST(@ExternalValue AS jsonb),external_revision=@ExternalRevision,external_deleted=@ExternalDeleted
            WHERE tenant_id=@TenantId AND connection_id=@connectionId AND entity_type=@EntityType AND local_id=@LocalId;
            UPDATE aurora_sync_outbox SET state='superseded',lease_id=NULL,lease_until=NULL
            WHERE tenant_id=@TenantId AND connection_id=@connectionId AND entity_type=@EntityType AND local_id=@LocalId AND state IN ('pending','conflict');
            UPDATE aurora_sync_conflict SET resolved_at=now()
            WHERE tenant_id=@TenantId AND connection_id=@connectionId AND entity_type=@EntityType AND local_id=@LocalId AND resolved_at IS NULL;
            """,new{tenant.TenantId,connectionId,conflict.EntityType,conflict.LocalId,conflict.ExternalValue,conflict.ExternalRevision,conflict.ExternalDeleted},tx,cancellationToken:ct));
        if(resolution.Deleted!=conflict.ExternalDeleted||!resolution.Deleted&&!SyncJson.Equal(saved.Data,JsonNode.Parse(conflict.ExternalValue)))
            await SyncStore.Enqueue(db,tx,tenant.TenantId,conflict.EntityType,saved.Id,saved.Revision,resolution.Deleted?"delete":"upsert",saved.Data.ToJsonString(),ct);
        await tx.CommitAsync(ct);
    }
    sealed record ConflictRow(string EntityType,string LocalId,string ExternalValue,string ExternalRevision,bool ExternalDeleted,long Sequence);
}
