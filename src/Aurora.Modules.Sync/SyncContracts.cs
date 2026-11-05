using System.Text.Json.Nodes;
using Npgsql;
namespace Aurora.Modules.Sync;

public sealed record SyncChange(Guid Id,Guid ConnectionId,string EntityType,string LocalId,long LocalRevision,string Operation,string Payload,int Attempts,Guid LeaseId);
public sealed record ExternalChange(string EventId,string EntityType,string ExternalId,string Revision,JsonObject Data,bool Deleted=false);
public sealed record ExternalReceipt(string ExternalId,string Revision);
public sealed record ExternalBatch(IReadOnlyList<ExternalChange> Changes,string? Checkpoint);

// Provider implementation owns Firebird SQL, native IDs, connection credentials and
// transactional idempotency receipts. The same change ID must never create twice.
public interface ISyncProvider
{
    Task<ExternalReceipt> Push(SyncChange change,CancellationToken ct);
    Task<ExternalBatch> Pull(string? checkpoint,CancellationToken ct);
}

// Import implementation must apply its change, mapping, conflict and inbox receipt
// in one Aurora transaction. Checkpoints advance only after the batch commits.
public interface IExternalChangeImporter
{
    Task Apply(Guid connectionId,ExternalBatch batch,CancellationToken ct);
}

// Queue-based providers acknowledge exact events only after the Aurora transaction
// commits. If acknowledgement fails, delivery repeats and the inbox deduplicates it.
public interface ISyncAcknowledgement
{
    Task Acknowledge(ExternalBatch batch,CancellationToken ct);
}

public sealed record SyncEntity(string Id,long Revision,JsonObject Data,bool Deleted);

// Implementations belong to the business module. Every read/write uses the supplied
// transaction, including relationship validation and invalidating planning snapshots.
public interface ISyncEntityStore
{
    Task<SyncEntity?> Read(string entityType,string localId,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct);
    Task<SyncEntity> Write(string entityType,string? localId,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct);
}

public sealed class SyncBusyException() : Exception("This record is being synchronized. Retry the incoming batch.");

public sealed class SyncConflictException(string message) : Exception(message);

public sealed record FieldMerge(JsonObject Result,string[] Conflicts);
public static class SyncMerge
{
    // A native transport snapshot owns every field it supplies. Retain supplemental
    // target metadata (e.g. execution state and line PO/BOL) absent from the source.
    // Match line supplements only by stable identity, never position or SKU.
    public static JsonObject ReplaceSourceFields(JsonObject local,JsonObject source)
    {
        var result=(JsonObject)local.DeepClone();
        foreach(var field in source)
        {
            if(field.Value is JsonObject child && local[field.Key] is JsonObject prior)
                result[field.Key]=ReplaceSourceFields(prior,child);
            else if(field.Key=="Lines" && field.Value is JsonArray lines && local[field.Key] is JsonArray old)
                result[field.Key]=new JsonArray(lines.Select(line=>
                {
                    if(line is not JsonObject row || !Guid.TryParse(row["LineId"]?.ToString(),out var id))return line?.DeepClone();
                    var matches=old.OfType<JsonObject>().Where(r=>Guid.TryParse(r["LineId"]?.ToString(),out var previous)&&previous==id).ToArray();
                    return matches.Length==1?ReplaceSourceFields(matches[0],row):row.DeepClone();
                }).ToArray());
            else result[field.Key]=field.Value?.DeepClone();
        }
        return result;
    }
    // Three-way merge: independent edits combine; competing edits remain unresolved.
    // Field absence and explicit null are distinct. Arrays are treated atomically.
    public static FieldMerge Apply(JsonObject baseline,JsonObject local,JsonObject external)
    {
        var merged=(JsonObject)local.DeepClone();
        var conflicts=new List<string>();
        foreach(var key in baseline.Select(p=>p.Key).Concat(local.Select(p=>p.Key)).Concat(external.Select(p=>p.Key)).Distinct(StringComparer.Ordinal))
        {
            bool Equal(JsonObject a,JsonObject b)=>a.ContainsKey(key)==b.ContainsKey(key)&&SyncJson.Equal(a[key],b[key],key);
            if(Equal(baseline,external)||Equal(local,external))continue;
            if(!Equal(baseline,local))
            {
                if(baseline[key] is JsonObject original && local[key] is JsonObject ours && external[key] is JsonObject theirs)
                {
                    var nested=Apply(original,ours,theirs);merged[key]=nested.Result;
                    conflicts.AddRange(nested.Conflicts.Select(field=>$"{key}.{field}"));
                }
                else conflicts.Add(key);
                continue;
            }
            if(external.ContainsKey(key))merged[key]=external[key]?.DeepClone();else merged.Remove(key);
        }
        return new(merged,conflicts.ToArray());
    }
}
