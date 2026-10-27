using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Sync;

public sealed class SyncImporter(NpgsqlConnectionFactory factory,ITenantContext tenant,ISyncEntityStore entities) : IExternalChangeImporter
{
    public async Task Apply(Guid connectionId,ExternalBatch batch,CancellationToken ct)
    {
        if(batch.Changes.Count>1000)throw new FormatException("Import batches allow up to 1,000 changes.");
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        // Serializes importers and configuration changes for this connection. Claim
        // also takes this lock before leasing, closing the pull/push race.
        if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT enabled FROM aurora_sync_connection WHERE tenant_id=@TenantId AND id=@connectionId FOR NO KEY UPDATE",new{tenant.TenantId,connectionId},tx,cancellationToken:ct)))
            throw new InvalidOperationException("The integration connection is not enabled.");
        foreach(var change in batch.Changes)
        {
            if(string.IsNullOrWhiteSpace(change.EventId)||string.IsNullOrWhiteSpace(change.EntityType)||string.IsNullOrWhiteSpace(change.ExternalId)||string.IsNullOrWhiteSpace(change.Revision))
                throw new FormatException("An incoming change is incomplete.");
            var fingerprint=Fingerprint(change);
            var prior=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT fingerprint FROM aurora_sync_inbox WHERE tenant_id=@TenantId AND connection_id=@connectionId AND event_id=@EventId",new{tenant.TenantId,connectionId,change.EventId},tx,cancellationToken:ct));
            if(prior is not null)
            {
                if(prior!=fingerprint)throw new FormatException("The provider reused an event ID for different data.");
                continue;
            }
            var mapping=await db.QuerySingleOrDefaultAsync<Mapping>(new CommandDefinition("SELECT local_id AS LocalId,baseline::text AS Baseline,external_deleted AS Deleted FROM aurora_sync_mapping WHERE tenant_id=@TenantId AND connection_id=@connectionId AND entity_type=@EntityType AND external_id=@ExternalId FOR UPDATE",new{tenant.TenantId,connectionId,change.EntityType,change.ExternalId},tx,cancellationToken:ct));
            var current=mapping is null?null:await entities.Read(change.EntityType,mapping.LocalId,db,tx,ct);
            if(mapping is not null && current is null)throw new InvalidOperationException("A mapped local record is missing; reconcile it before importing.");
            if(current is not null && await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_sync_outbox WHERE tenant_id=@TenantId AND connection_id=@connectionId AND entity_type=@EntityType AND local_id=@Id AND state='working')",new{tenant.TenantId,connectionId,change.EntityType,current.Id},tx,cancellationToken:ct)))throw new SyncBusyException();
            var baseline=mapping is null?new JsonObject():JsonNode.Parse(mapping.Baseline)!.AsObject();
            var local=current?.Data??new JsonObject();
            var merge=SyncMerge.Apply(baseline,local,change.Data);
            // A deletion must not erase concurrent local edits. Likewise a local
            // deletion cannot be silently resurrected by an incoming update.
            var fields=merge.Conflicts.ToList();
            var localDeletionChanged=current is not null && current.Deleted!=mapping!.Deleted;
            var externalDeletionChanged=change.Deleted!=(mapping?.Deleted??false);
            var mergedDeleted=localDeletionChanged?current!.Deleted:change.Deleted;
            if(current is not null && current.Deleted!=change.Deleted &&
                (externalDeletionChanged&&change.Deleted&&!SyncJson.Equal(baseline,local) || localDeletionChanged&&current.Deleted&&!SyncJson.Equal(baseline,change.Data)))fields.Add("$deleted");
            var unresolved=current is not null && await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_sync_conflict WHERE tenant_id=@TenantId AND connection_id=@connectionId AND entity_type=@EntityType AND local_id=@Id AND resolved_at IS NULL)",new{tenant.TenantId,connectionId,change.EntityType,current.Id},tx,cancellationToken:ct));
            if(unresolved)fields.Add("$unresolved");
            // Unmapped records are new external records, not concurrent creations.
            if(current is null){merge=new((JsonObject)change.Data.DeepClone(),[]);fields.Clear();}
            if(fields.Count>0)
            {
                await db.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO aurora_sync_conflict(tenant_id,connection_id,entity_type,local_id,external_id,local_value,external_value,fields,external_revision,local_revision,local_deleted,external_deleted,event_id)
                    VALUES(@TenantId,@connectionId,@EntityType,@Id,@ExternalId,CAST(@local AS jsonb),CAST(@external AS jsonb),CAST(@fields AS jsonb),@Revision,@localRevision,@localDeleted,@Deleted,@EventId)
                    """,new{tenant.TenantId,connectionId,change.EntityType,Id=current!.Id,change.ExternalId,local=local.ToJsonString(),external=change.Data.ToJsonString(),fields=System.Text.Json.JsonSerializer.Serialize(fields.Distinct()),change.Revision,localRevision=current.Revision,localDeleted=current.Deleted,change.Deleted,change.EventId},tx,cancellationToken:ct));
            }
            else
            {
                var saved=current is not null && current.Deleted==mergedDeleted && SyncJson.Equal(current.Data,merge.Result)
                    ? current : await entities.Write(change.EntityType,current?.Id,merge.Result,mergedDeleted,db,tx,ct);
                await db.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO aurora_sync_mapping(tenant_id,connection_id,entity_type,local_id,external_id,external_revision,baseline,external_deleted)
                    VALUES(@TenantId,@connectionId,@EntityType,@Id,@ExternalId,@Revision,CAST(@baseline AS jsonb),@Deleted)
                    ON CONFLICT(tenant_id,connection_id,entity_type,local_id) DO UPDATE SET external_revision=excluded.external_revision,baseline=excluded.baseline,external_deleted=excluded.external_deleted;
                    UPDATE aurora_sync_outbox SET state='superseded',lease_id=NULL,lease_until=NULL
                    WHERE tenant_id=@TenantId AND connection_id=@connectionId AND entity_type=@EntityType AND local_id=@Id AND state IN ('pending','conflict');
                    """,new{tenant.TenantId,connectionId,change.EntityType,saved.Id,change.ExternalId,change.Revision,change.Deleted,baseline=change.Data.ToJsonString()},tx,cancellationToken:ct));
                if(saved.Deleted!=change.Deleted||!saved.Deleted&&!SyncJson.Equal(saved.Data,change.Data))
                    await SyncStore.Enqueue(db,tx,tenant.TenantId,change.EntityType,saved.Id,saved.Revision,saved.Deleted?"delete":"upsert",saved.Data.ToJsonString(),ct);
            }
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_sync_inbox(tenant_id,connection_id,event_id,fingerprint) VALUES(@TenantId,@connectionId,@EventId,@fingerprint)",new{tenant.TenantId,connectionId,change.EventId,fingerprint},tx,cancellationToken:ct));
        }
        await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET checkpoint=@Checkpoint,last_success_at=now() WHERE tenant_id=@TenantId AND id=@connectionId",new{tenant.TenantId,connectionId,batch.Checkpoint},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }

    static string Fingerprint(ExternalChange change)
    {
        // Canonicalize property order so equivalent JSON produces the same receipt.
        static JsonNode? Sort(JsonNode? node)=>node switch {
            JsonObject obj=>new JsonObject(obj.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>KeyValuePair.Create(x.Key,Sort(x.Value)))),
            JsonArray array=>new JsonArray(array.Select(Sort).ToArray()),_=>node?.DeepClone()};
        var envelope=new JsonArray(change.EntityType,change.ExternalId,change.Revision,change.Deleted,Sort(change.Data));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.ToJsonString())));
    }
    sealed record Mapping(string LocalId,string Baseline,bool Deleted);
}
