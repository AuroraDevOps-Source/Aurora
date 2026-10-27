using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova.Sync;

public sealed record NovaSyncSnapshot(string EntityType,string ExternalId,string Revision,JsonObject Data,bool Deleted=false)
{
    public DateOnly? BusinessDate {get;init;}
    public bool Open {get;init;}
    public bool RequiredByCurrentWork {get;init;}
}

// Business mappings validate the external baseline and write using this exact
// Firebird transaction. They must never call the legacy per-request CRUD methods,
// which own separate transactions and therefore cannot share the receipt.
public interface INovaSyncMapper
{
    Task<ExternalReceipt> Push(SyncChange change,FbConnection db,FbTransaction tx,CancellationToken ct);
    Task<IReadOnlyList<NovaSyncSnapshot>> Read(NovaChangeEvent change,FbConnection db,FbTransaction tx,CancellationToken ct);
}

public sealed class NovaSyncProvider(Guid consumerId,string connectionString,INovaSyncMapper mapper,TimeProvider? clock=null,TimeZoneInfo? timeZone=null):ISyncProvider,ISyncAcknowledgement
{
    public async Task<ExternalReceipt> Push(SyncChange change,CancellationToken ct)
    {
        if(change.ConnectionId!=consumerId)throw new InvalidOperationException("Sync change belongs to another connection.");
        await using var db=new FbConnection(connectionString);await db.OpenAsync(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait,WaitTimeout=TimeSpan.FromSeconds(10)});
        // The consumer lock serializes receipt lookup with receipt creation, including
        // retries after a lost HTTP/DB response and expired Aurora worker leases.
        var enabled=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT ENABLED FROM AURORA_SYNC_CONSUMER WHERE CONSUMER_ID=@consumer WITH LOCK",new{consumer=consumerId.ToString()},tx,cancellationToken:ct));
        if(enabled!="Y")throw new InvalidOperationException("Nova synchronization is not enabled for this connection.");
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{change.EntityType,change.LocalId,change.LocalRevision,change.Operation,change.Payload}))));
        var prior=await db.QuerySingleOrDefaultAsync<Receipt>(new CommandDefinition("SELECT TRIM(ENTITY_ID) AS ExternalId,TRIM(EXTERNAL_REVISION) AS Revision,TRIM(PAYLOAD_HASH) AS PayloadHash FROM AURORA_SYNC_RECEIPT WHERE CONSUMER_ID=@consumer AND CHANGE_ID=@id",new{consumer=consumerId.ToString(),id=change.Id.ToString()},tx,cancellationToken:ct));
        if(prior is not null)
        {
            if(prior.PayloadHash!=hash)throw new SyncConflictException("An acknowledged change ID was reused for different data.");
            await tx.CommitAsync(ct);
            return new(prior.ExternalId,prior.Revision);
        }
        await NovaChangeQueue.SetOrigin(db,tx,consumerId,ct);
        var receipt=await mapper.Push(change,db,tx,ct);
        if(string.IsNullOrWhiteSpace(receipt.ExternalId)||receipt.ExternalId.Length>80||string.IsNullOrWhiteSpace(receipt.Revision)||receipt.Revision.Length>128)throw new FormatException("The Nova mapping returned an invalid receipt.");
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AURORA_SYNC_RECEIPT(CONSUMER_ID,CHANGE_ID,ENTITY_TYPE,ENTITY_ID,EXTERNAL_REVISION,PAYLOAD_HASH)
            VALUES(@consumer,@id,@EntityType,@ExternalId,@Revision,@hash)
            """,new{consumer=consumerId.ToString(),id=change.Id.ToString(),change.EntityType,receipt.ExternalId,receipt.Revision,hash},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return receipt;
    }

    public async Task<ExternalBatch> Pull(string? checkpoint,CancellationToken ct)
    {
        await using var db=new FbConnection(connectionString);await db.OpenAsync(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait});
        if(await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT ENABLED FROM AURORA_SYNC_CONSUMER WHERE CONSUMER_ID=@consumer",new{consumer=consumerId.ToString()},tx,cancellationToken:ct))!="Y")throw new InvalidOperationException("Nova synchronization is not enabled for this connection.");
        var events=await NovaChangeQueue.Read(db,tx,consumerId,ct);
        var changes=new List<ExternalChange>();
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime((clock??TimeProvider.System).GetUtcNow(),timeZone??TimeZoneInfo.Utc).DateTime);
        foreach(var item in events)
        {
            var snapshots=await mapper.Read(item,db,tx,ct);
            foreach(var snapshot in snapshots)
            {
                // Deletions still propagate to records already mirrored in Aurora.
                // An out-of-window update is acknowledged only with the committed
                // batch, so unsupported/failed mappings are never silently dropped.
                if(!snapshot.Deleted&&!SyncHistoryWindow.Includes(snapshot.EntityType,snapshot.BusinessDate,snapshot.Open,snapshot.RequiredByCurrentWork,today))continue;
                // Events point at aggregates. A retry may read a newer committed
                // snapshot, so include its revision in the immutable inbox identity.
                var identity=JsonSerializer.Serialize(new{item.EventId,snapshot.EntityType,snapshot.ExternalId,snapshot.Revision,snapshot.Deleted});
                var eventId=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                changes.Add(new(eventId,snapshot.EntityType,snapshot.ExternalId,snapshot.Revision,snapshot.Data,snapshot.Deleted));
            }
        }
        await tx.CommitAsync(ct);
        // Exact queue IDs, not a generator watermark. The caller never acknowledges
        // this token until all imported data/inbox/conflicts have committed.
        return new(changes,JsonSerializer.Serialize(new Ack(consumerId,events.Select(e=>e.EventId).ToArray())));
    }

    public async Task Acknowledge(ExternalBatch batch,CancellationToken ct)
    {
        var ack=JsonSerializer.Deserialize<Ack>(batch.Checkpoint??"")??throw new FormatException("Missing Nova acknowledgement token.");
        if(ack.Consumer!=consumerId||ack.Events.Length>200||ack.Events.Any(id=>id<=0))throw new FormatException("Invalid Nova acknowledgement token.");
        await using var db=new FbConnection(connectionString);await db.OpenAsync(ct);
        await using var tx=db.BeginTransaction();
        await NovaChangeQueue.Acknowledge(db,tx,consumerId,ack.Events,ct);
        await tx.CommitAsync(ct);
    }
    sealed record Ack(Guid Consumer,long[] Events);
    sealed record Receipt(string ExternalId,string Revision,string PayloadHash);
}
