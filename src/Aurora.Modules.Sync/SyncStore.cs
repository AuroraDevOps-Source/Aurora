using System.Text.Json;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Sync;

public sealed class SyncStore(NpgsqlConnectionFactory factory,ITenantContext tenant)
{
    // The caller MUST pass the business transaction. A rolled-back save cannot
    // leave a queued write, and a committed save cannot lose its queued write.
    public static async Task Enqueue(NpgsqlConnection db,NpgsqlTransaction tx,Guid tenantId,
        string entityType,string localId,long revision,string operation,string payload,CancellationToken ct)
    {
        if(tx.Connection!=db)throw new ArgumentException("Use the business save transaction.");
        if(string.IsNullOrWhiteSpace(entityType)||string.IsNullOrWhiteSpace(localId)||revision<0||operation is not ("upsert" or "delete"))throw new FormatException("Invalid sync change.");
        using var parsed=JsonDocument.Parse(payload);
        if(parsed.RootElement.ValueKind!=JsonValueKind.Object)throw new FormatException("Sync payload must be an object.");
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_sync_outbox(tenant_id,connection_id,entity_type,local_id,local_revision,operation,payload)
            SELECT @tenantId,id,@entityType,@localId,@revision,@operation,CAST(@payload AS jsonb)
            FROM aurora_sync_connection WHERE tenant_id=@tenantId
            ON CONFLICT(tenant_id,connection_id,entity_type,local_id,local_revision) DO NOTHING
            """,new{tenantId,entityType,localId,revision,operation,payload},tx,cancellationToken:ct));
        var mismatch=await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_sync_outbox WHERE tenant_id=@tenantId AND entity_type=@entityType
              AND local_id=@localId AND local_revision=@revision AND (operation<>@operation OR payload<>CAST(@payload AS jsonb)))
            """,new{tenantId,entityType,localId,revision,operation,payload},tx,cancellationToken:ct));
        if(mismatch)throw new FormatException("The same record revision cannot contain different sync changes.");
    }

    public async Task<SyncChange?> Claim(Guid connectionId,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        var leaseId=Guid.NewGuid();
        await db.ExecuteAsync(new CommandDefinition("SELECT id FROM aurora_sync_connection WHERE tenant_id=@TenantId AND id=@connectionId FOR NO KEY UPDATE",new{tenant.TenantId,connectionId},tx,cancellationToken:ct));
        var change=await db.QuerySingleOrDefaultAsync<SyncChange>(new CommandDefinition("""
            WITH candidate AS (
              SELECT o.id FROM aurora_sync_outbox o JOIN aurora_sync_connection c ON c.tenant_id=o.tenant_id AND c.id=o.connection_id
              WHERE o.tenant_id=@TenantId AND o.connection_id=@connectionId AND c.enabled
                AND ((o.state='pending' AND o.available_at<=now()) OR (o.state='working' AND o.lease_until<now()))
                AND NOT EXISTS(SELECT 1 FROM aurora_sync_outbox earlier WHERE earlier.tenant_id=o.tenant_id
                  AND earlier.connection_id=o.connection_id AND earlier.entity_type=o.entity_type AND earlier.local_id=o.local_id
                  AND earlier.sequence<o.sequence AND earlier.state NOT IN ('synced','superseded'))
                AND NOT EXISTS(SELECT 1 FROM aurora_sync_conflict conflict WHERE conflict.tenant_id=o.tenant_id
                  AND conflict.connection_id=o.connection_id AND conflict.entity_type=o.entity_type
                  AND conflict.local_id=o.local_id AND conflict.resolved_at IS NULL)
              ORDER BY o.sequence LIMIT 1 FOR UPDATE OF o SKIP LOCKED
            )
            UPDATE aurora_sync_outbox o SET state='working',lease_id=@leaseId,lease_until=now()+interval '2 minutes',attempts=attempts+1
            FROM candidate WHERE o.tenant_id=@TenantId AND o.id=candidate.id
            RETURNING o.id,o.connection_id AS ConnectionId,o.entity_type AS EntityType,o.local_id AS LocalId,
              o.local_revision AS LocalRevision,o.operation,o.payload::text AS Payload,o.attempts,o.lease_id AS LeaseId
            """,new{tenant.TenantId,connectionId,leaseId},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return change;
    }

    public async Task Complete(SyncChange change,ExternalReceipt receipt,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(receipt.ExternalId)||string.IsNullOrWhiteSpace(receipt.Revision))throw new FormatException("External receipt is incomplete.");
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        var count=await db.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_sync_outbox SET state='synced',synced_at=now(),last_error=NULL,lease_id=NULL,lease_until=NULL
            WHERE tenant_id=@TenantId AND id=@Id AND connection_id=@ConnectionId AND state='working' AND lease_id=@LeaseId
            """,new{tenant.TenantId,change.Id,change.ConnectionId,change.LeaseId},tx,cancellationToken:ct));
        if(count!=1)throw new InvalidOperationException("The sync lease is no longer owned by this worker.");
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_sync_mapping(tenant_id,connection_id,entity_type,local_id,external_id,external_revision,baseline,external_deleted)
            VALUES(@TenantId,@ConnectionId,@EntityType,@LocalId,@ExternalId,@Revision,CAST(@Payload AS jsonb),@deleted)
            ON CONFLICT(tenant_id,connection_id,entity_type,local_id) DO UPDATE
              SET external_id=excluded.external_id,external_revision=excluded.external_revision,baseline=excluded.baseline,external_deleted=excluded.external_deleted;
            UPDATE aurora_sync_connection SET last_success_at=now() WHERE tenant_id=@TenantId AND id=@ConnectionId;
            """,new{tenant.TenantId,change.ConnectionId,change.EntityType,change.LocalId,receipt.ExternalId,receipt.Revision,change.Payload,deleted=change.Operation=="delete"},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }

    public async Task Retry(SyncChange change,bool conflict,CancellationToken ct,string? reason=null)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var seconds=Math.Min(300,Math.Pow(2,Math.Min(change.Attempts,8)));
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_sync_outbox SET state=@state,available_at=now()+@seconds*interval '1 second',lease_id=NULL,lease_until=NULL,last_error=@error
            WHERE tenant_id=@TenantId AND id=@Id AND connection_id=@ConnectionId AND lease_id=@LeaseId AND state='working'
            """,new{tenant.TenantId,change.Id,change.ConnectionId,change.LeaseId,seconds,state=conflict?"conflict":"pending",
                error=reason ?? (conflict?"Both systems changed this record. Review the conflict before retrying.":"The external update was not confirmed. It will be retried.")},cancellationToken:ct));
    }
}
