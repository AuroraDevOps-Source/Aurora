using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Modules.Sync;

namespace Aurora.Integrations.FreightOps;

// Explicit v1 envelope. Data is the agreed shared projection, never a serialized
// FreightOps aggregate or the legacy TMSImport DTO. Business mapping lives at the target.
public sealed record FreightOpsWrite(int ProtocolVersion,string SourceIdentity,Guid ConnectionId,
    Guid ChangeId,string EntityType,string LocalId,long LocalRevision,string Operation,
    string? ExternalId,string? ExpectedRevision,JsonObject Data);
public sealed record FreightOpsReceipt(int ProtocolVersion,string SourceIdentity,Guid ConnectionId,
    Guid ChangeId,string RequestHash,string ExternalId,string Revision);
public sealed record FreightOpsPull(int ProtocolVersion,string SourceIdentity,Guid ConnectionId,int Limit);
public sealed record FreightOpsEvent(string EventId,string EntityType,string ExternalId,string Revision,JsonObject Data,bool Deleted);
public sealed record FreightOpsBatch(int ProtocolVersion,string SourceIdentity,Guid ConnectionId,FreightOpsEvent[] Events);
public sealed record FreightOpsAck(int ProtocolVersion,string SourceIdentity,Guid ConnectionId,string[] EventIds,Dictionary<string,string>? Bindings=null);
public sealed record FreightOpsAckReceipt(int ProtocolVersion,string SourceIdentity,Guid ConnectionId,string[] EventIds);

public interface IFreightOpsRequestStore
{
    // Must durably freeze the target baseline AND payload before the first network call.
    // A response-loss retry must not rebuild the request using a newer imported baseline.
    Task<FreightOpsWrite> Prepare(SyncChange change,string sourceIdentity,CancellationToken ct);
    Task<string?> ImportedLocalId(Guid connectionId,string kind,string externalId,CancellationToken ct)=>Task.FromResult<string?>(null);
}

public static class FreightOpsProtocol
{
    public const int Version=1;
    public const int BatchSize=200;
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static string Hash(FreightOpsWrite request)=>Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request,Json))));
    internal static void Text(string? value,string field,int maximum=128)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>maximum||value.Any(char.IsControl))
            throw new FormatException($"Invalid FreightOps {field}.");
    }
}
