using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aurora.Modules.Sync;

namespace Aurora.Integrations.FreightOps;

// Used by the opt-in runtime and explicit enrollment. Disabled until configured.
public sealed class FreightOpsSyncProvider : ISyncProvider,ISyncAcknowledgement,IDisposable
{
    readonly Guid connectionId;
    readonly string sourceIdentity;
    readonly IFreightOpsRequestStore requests;
    readonly Func<CancellationToken,Task<string>> accessToken;
    readonly HttpClient http;
    const int MaximumResponseBytes=4*1024*1024;

    public FreightOpsSyncProvider(Guid connectionId,string sourceIdentity,Uri installation,
        IFreightOpsRequestStore requests,Func<CancellationToken,Task<string>> accessToken)
    {
        if(connectionId==Guid.Empty)throw new ArgumentException("Connection is required.");
        FreightOpsProtocol.Text(sourceIdentity,"source identity");
        if(!installation.IsAbsoluteUri || !string.IsNullOrEmpty(installation.UserInfo) ||
            !string.IsNullOrEmpty(installation.Query) || !string.IsNullOrEmpty(installation.Fragment) ||
            (installation.Scheme!="https" && !(installation.Scheme=="http" && installation.IsLoopback)))
            throw new ArgumentException("Use an HTTPS installation URL (HTTP is allowed only for local tests).");
        this.connectionId=connectionId;this.sourceIdentity=sourceIdentity;
        this.requests=requests;this.accessToken=accessToken;
        // Never follow redirects with credentials or replay a write at a new destination.
        http=new HttpClient(new SocketsHttpHandler{AllowAutoRedirect=false,UseCookies=false})
        {
            BaseAddress=new Uri(installation.AbsoluteUri.TrimEnd('/')+"/api/aurora-sync/v1/"),
            Timeout=TimeSpan.FromSeconds(60)
        };
    }

    public async Task<ExternalReceipt> Push(SyncChange change,CancellationToken ct)
    {
        if(change.ConnectionId!=connectionId)throw new InvalidOperationException("Wrong FreightOps connection.");
        var request=await requests.Prepare(change,sourceIdentity,ct);
        Envelope(request.ProtocolVersion,request.SourceIdentity,request.ConnectionId);
        if(request.ChangeId!=change.Id)throw new InvalidOperationException("Prepared request belongs to another change.");
        var receipt=await Send<FreightOpsReceipt>("changes",request,ct);
        Envelope(receipt.ProtocolVersion,receipt.SourceIdentity,receipt.ConnectionId);
        if(receipt.ChangeId!=change.Id||receipt.RequestHash!=FreightOpsProtocol.Hash(request))
            throw new FormatException("FreightOps receipt does not match the submitted request.");
        FreightOpsProtocol.Text(receipt.ExternalId,"external ID",80);
        FreightOpsProtocol.Text(receipt.Revision,"revision");
        return new(receipt.ExternalId,receipt.Revision);
    }

    // Administrative cohort enrollment is explicit and version-checked. The caller
    // retains and replays the same change ID/body after an uncertain response.
    public async Task<ExternalReceipt> Enroll(FreightOpsWrite request,CancellationToken ct)
    {
        Envelope(request.ProtocolVersion,request.SourceIdentity,request.ConnectionId);
        if(request.ChangeId==Guid.Empty||request.Operation!="enroll"||request.EntityType!="order"||request.ExternalId is null||request.ExpectedRevision is null)
            throw new FormatException("Enrollment requires a change ID, explicit PRO and expected stream revision.");
        var receipt=await Send<FreightOpsReceipt>("enroll",request,ct);
        Envelope(receipt.ProtocolVersion,receipt.SourceIdentity,receipt.ConnectionId);
        if(receipt.ChangeId!=request.ChangeId||receipt.RequestHash!=FreightOpsProtocol.Hash(request)||receipt.ExternalId!=request.ExternalId)
            throw new FormatException("FreightOps enrollment receipt does not match the request.");
        FreightOpsProtocol.Text(receipt.Revision,"revision");
        return new(receipt.ExternalId,receipt.Revision);
    }

    public async Task<ExternalBatch> Pull(string? checkpoint,CancellationToken ct)
    {
        // Checkpoints contain exact acknowledgement IDs, never a sequence watermark.
        // Always re-read pending events: the previous acknowledgement may have been lost.
        if(checkpoint is not null)ValidateAck(ParseAck(checkpoint));
        var batch=await Send<FreightOpsBatch>("pull",new FreightOpsPull(1,sourceIdentity,connectionId,FreightOpsProtocol.BatchSize),ct);
        Envelope(batch.ProtocolVersion,batch.SourceIdentity,batch.ConnectionId);
        if(batch.Events is null||batch.Events.Length>FreightOpsProtocol.BatchSize)throw new FormatException("Invalid FreightOps batch size.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var item in batch.Events)
        {
            if(item is null||item.Data is null)throw new FormatException("Incomplete FreightOps event.");
            FreightOpsProtocol.Text(item.EventId,"event ID");
            FreightOpsProtocol.Text(item.EntityType,"entity type",40);
            if(item.EntityType!="order")throw new FormatException("FreightOps returned an unsupported entity type.");
            FreightOpsProtocol.Text(item.ExternalId,"external ID",80);
            FreightOpsProtocol.Text(item.Revision,"revision");
            if(!ids.Add(item.EventId))throw new FormatException("Duplicate FreightOps event ID in batch.");
        }
        var ack=new FreightOpsAck(1,sourceIdentity,connectionId,batch.Events.Select(e=>e.EventId).ToArray());
        return new(batch.Events.Select(e=>new ExternalChange(e.EventId,e.EntityType,e.ExternalId,e.Revision,e.Data,e.Deleted)).ToArray(),
            JsonSerializer.Serialize(ack,FreightOpsProtocol.Json));
    }

    public async Task Acknowledge(ExternalBatch batch,CancellationToken ct)
    {
        var ack=ParseAck(batch.Checkpoint);ValidateAck(ack);
        if(!ack.EventIds.SequenceEqual(batch.Changes.Select(e=>e.EventId),StringComparer.Ordinal))
            throw new FormatException("FreightOps acknowledgement does not match the imported batch.");
        if(ack.EventIds.Length==0)return;
        var receipt=await Send<FreightOpsAckReceipt>("acknowledge",ack,ct);
        Envelope(receipt.ProtocolVersion,receipt.SourceIdentity,receipt.ConnectionId);
        if(receipt.EventIds is null||!receipt.EventIds.SequenceEqual(ack.EventIds,StringComparer.Ordinal))
            throw new FormatException("FreightOps did not confirm the exact acknowledged events.");
    }

    void Envelope(int version,string source,Guid connection)
    {
        if(version!=1||source!=sourceIdentity||connection!=connectionId)
            throw new FormatException("FreightOps protocol, source generation, or connection does not match. Reconciliation is required.");
    }
    void ValidateAck(FreightOpsAck ack)
    {
        Envelope(ack.ProtocolVersion,ack.SourceIdentity,ack.ConnectionId);
        if(ack.EventIds is null||ack.EventIds.Length>FreightOpsProtocol.BatchSize||ack.EventIds.Distinct(StringComparer.Ordinal).Count()!=ack.EventIds.Length)
            throw new FormatException("Invalid FreightOps acknowledgement.");
        foreach(var id in ack.EventIds)FreightOpsProtocol.Text(id,"event ID");
    }
    static FreightOpsAck ParseAck(string? value)
    {
        try{return JsonSerializer.Deserialize<FreightOpsAck>(value??"",FreightOpsProtocol.Json)??throw new FormatException("Missing FreightOps checkpoint.");}
        catch(JsonException){throw new FormatException("Invalid FreightOps checkpoint.");}
    }
    async Task<T> Send<T>(string path,object body,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));ct=timeout.Token;
        var token=await accessToken(ct);
        if(string.IsNullOrWhiteSpace(token))throw new InvalidOperationException("FreightOps access token is unavailable.");
        var bytes=JsonSerializer.SerializeToUtf8Bytes(body,body.GetType(),FreightOpsProtocol.Json);
        if(bytes.Length>MaximumResponseBytes)throw new FormatException("FreightOps request is too large.");
        using var message=new HttpRequestMessage(HttpMethod.Post,path){Content=new ByteArrayContent(bytes)};
        message.Content.Headers.ContentType=new MediaTypeHeaderValue("application/json"){CharSet="utf-8"};
        if(body is FreightOpsWrite write)message.Headers.Add("X-Aurora-Request-Hash",FreightOpsProtocol.Hash(write));
        message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,ct);
        if(response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
            throw new SyncConflictException("FreightOps rejected the target revision or change identity. Import and review before retrying.");
        if(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            throw new FormatException("FreightOps rejected this synchronization operation or its mapped fields.");
        // Authentication, throttling and outages remain retryable. Never include remote
        // HTML, business data, credentials, or exception bodies in persisted diagnostics.
        if(response.StatusCode!=HttpStatusCode.OK)throw new HttpRequestException("FreightOps request was not confirmed.",null,response.StatusCode);
        if(response.Content.Headers.ContentType?.MediaType!="application/json")throw new FormatException("FreightOps response must be JSON.");
        if(response.Content.Headers.ContentLength>MaximumResponseBytes)throw new FormatException("FreightOps response is too large.");
        await using var input=await response.Content.ReadAsStreamAsync(ct);
        using var buffer=new MemoryStream();var chunk=new byte[8192];
        int read;
        while((read=await input.ReadAsync(chunk,ct))>0)
        {
            if(buffer.Length+read>MaximumResponseBytes)throw new FormatException("FreightOps response is too large.");
            buffer.Write(chunk,0,read);
        }
        try{return JsonSerializer.Deserialize<T>(buffer.ToArray(),FreightOpsProtocol.Json)??throw new FormatException("Empty FreightOps response.");}
        catch(JsonException){throw new FormatException("Malformed FreightOps response.");}
    }
    public void Dispose()=>http.Dispose();
}
