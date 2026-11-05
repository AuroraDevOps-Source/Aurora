using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;

namespace Aurora.Integrations.FreightOps;

public sealed class FreightOpsRequestStore(NpgsqlConnectionFactory factory,ITenantContext tenant):IFreightOpsRequestStore
{
    public async Task<string?> ImportedLocalId(Guid connectionId,string kind,string externalId,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT local_id FROM aurora_sync_mapping WHERE tenant_id=@TenantId AND connection_id=@connectionId AND entity_type=@kind AND external_id=@externalId",new{tenant.TenantId,connectionId,kind,externalId},cancellationToken:ct));
    }
    public async Task<FreightOpsWrite> Prepare(SyncChange change,string sourceIdentity,CancellationToken ct)
    {
        if(change.Id==Guid.Empty||change.ConnectionId==Guid.Empty||change.LocalRevision<0||change.Operation is not ("upsert" or "delete"))
            throw new FormatException("Invalid FreightOps change.");
        FreightOpsProtocol.Text(sourceIdentity,"source identity");
        FreightOpsProtocol.Text(change.EntityType,"entity type",40);
        FreightOpsProtocol.Text(change.LocalId,"local ID",128);
        JsonObject data;
        try{data=JsonNode.Parse(change.Payload) as JsonObject??throw new FormatException("FreightOps payload must be an object.");}
        catch(JsonException){throw new FormatException("Invalid FreightOps payload.");}
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        var source=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
            SELECT source_identity FROM aurora_sync_connection
            WHERE tenant_id=@TenantId AND id=@ConnectionId AND provider='freightops' AND enabled
            FOR NO KEY UPDATE
            """,new{tenant.TenantId,change.ConnectionId},tx,cancellationToken:ct));
        if(source!=sourceIdentity)throw new InvalidOperationException("FreightOps connection is disabled, unavailable, or points to another source.");
        var valid=await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_sync_outbox WHERE tenant_id=@TenantId AND id=@Id
              AND connection_id=@ConnectionId AND state='working' AND lease_id=@LeaseId AND lease_until>now()
              AND entity_type=@EntityType AND local_id=@LocalId AND local_revision=@LocalRevision
              AND operation=@Operation AND payload=CAST(@Payload AS jsonb))
            """,new{tenant.TenantId,change.Id,change.ConnectionId,change.LeaseId,change.EntityType,change.LocalId,change.LocalRevision,change.Operation,change.Payload},tx,cancellationToken:ct));
        if(!valid)throw new InvalidOperationException("The FreightOps outbox lease or payload does not match.");
        var stored=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
            SELECT request::text FROM aurora_freightops_request
            WHERE tenant_id=@TenantId AND connection_id=@ConnectionId AND change_id=@Id
            """,new{tenant.TenantId,change.ConnectionId,change.Id},tx,cancellationToken:ct));
        FreightOpsWrite request;
        if(stored is not null)
        {
            request=JsonSerializer.Deserialize<FreightOpsWrite>(stored,FreightOpsProtocol.Json)!;
            if(request.SourceIdentity!=sourceIdentity||request.EntityType!=change.EntityType||request.LocalId!=change.LocalId||
                request.LocalRevision!=change.LocalRevision||request.Operation!=change.Operation||!SamePayload(request.Data,data,change.EntityType))
                throw new SyncConflictException("The prepared FreightOps request cannot change. Reconcile the prior delivery first.");
        }
        else
        {
            var mapping=await db.QuerySingleOrDefaultAsync<Mapping>(new CommandDefinition("""
                SELECT external_id AS ExternalId,external_revision AS Revision,external_deleted AS Deleted
                FROM aurora_sync_mapping WHERE tenant_id=@TenantId AND connection_id=@ConnectionId
                  AND entity_type=@EntityType AND local_id=@LocalId
                """,new{tenant.TenantId,change.ConnectionId,change.EntityType,change.LocalId},tx,cancellationToken:ct));
            if(mapping is not null)
            {
                FreightOpsProtocol.Text(mapping.ExternalId,"mapped external ID",80);
                FreightOpsProtocol.Text(mapping.Revision,"mapped revision");
                if(mapping.Deleted&&change.Operation!="delete")throw new SyncConflictException("FreightOps removed this record. Review before restoring it.");
            }
            if(change.EntityType=="manifest")
            {
                var orders=data["Orders"] as JsonArray??throw new FormatException("Manifest orders are required.");
                var resolved=new JsonArray();
                foreach(var order in change.Operation=="delete"?new JsonArray():orders)
                {
                    var local=$"{Guid.Parse(order!["SourceId"]!.GetValue<string>()):D}/{order["Id"]!.GetValue<string>()}";
                    var mapped=await db.QuerySingleOrDefaultAsync<Mapping>(new CommandDefinition("SELECT external_id AS ExternalId,external_revision AS Revision,external_deleted AS Deleted FROM aurora_sync_mapping WHERE tenant_id=@TenantId AND connection_id=@ConnectionId AND entity_type='order' AND local_id=@local",new{tenant.TenantId,change.ConnectionId,local},tx,cancellationToken:ct));
                    if(mapped is null||mapped.Deleted)throw new SyncConflictException("Publish the manifest's shipments to FreightOps first.");
                    var unsettled=await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_sync_outbox WHERE tenant_id=@TenantId AND connection_id=@ConnectionId AND entity_type='order' AND local_id=@local AND state NOT IN ('synced','superseded')) OR EXISTS(SELECT 1 FROM aurora_sync_conflict WHERE tenant_id=@TenantId AND connection_id=@ConnectionId AND entity_type='order' AND local_id=@local AND resolved_at IS NULL)",new{tenant.TenantId,change.ConnectionId,local},tx,cancellationToken:ct));
                    if(unsettled)throw new SyncConflictException("Wait for shipment synchronization and resolve its conflicts before publishing the manifest.");
                    resolved.Add(new JsonObject{["LocalId"]=local,["ExternalId"]=mapped.ExternalId,["ExpectedRevision"]=mapped.Revision});
                }
                data["FreightOpsOrders"]=resolved;
            }
            request=new(1,sourceIdentity,change.ConnectionId,change.Id,change.EntityType,change.LocalId,change.LocalRevision,
                change.Operation,mapping?.ExternalId,mapping?.Revision,data);
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO aurora_freightops_request(tenant_id,connection_id,change_id,request)
                VALUES(@TenantId,@ConnectionId,@Id,@json)
                """,new{tenant.TenantId,change.ConnectionId,change.Id,json=JsonSerializer.Serialize(request,FreightOpsProtocol.Json)},tx,cancellationToken:ct));
        }
        await tx.CommitAsync(ct);
        return request;
    }
    sealed record Mapping(string ExternalId,string? Revision,bool Deleted);
    static bool SamePayload(JsonObject prepared,JsonObject original,string kind)
    {
        var copy=(JsonObject)prepared.DeepClone();
        if(kind=="manifest")copy.Remove("FreightOpsOrders");
        return JsonNode.DeepEquals(copy,original);
    }
}
