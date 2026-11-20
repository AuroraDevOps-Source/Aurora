using System.Threading.Channels;
using Aurora.Contracts;
using Aurora.Infrastructure.Tenancy;

namespace Aurora.Api.Features.Routing;

internal sealed class ConversionWorkerIdentity
{
    public ITenantContext? Identity { get; set; }
}

internal sealed class PdfConversionJobs(IServiceScopeFactory scopes, ILogger<PdfConversionJobs> log) : BackgroundService
{
    private sealed record Owner(Guid? TenantIdOrNull, Guid? UserIdOrNull) : ITenantContext;
    private sealed class Job(Owner owner, string name, byte[] bytes)
    {
        public Owner Owner { get; } = owner;
        public string Name { get; } = name;
        public List<OrderDocumentDraft> Ready { get; } = [];
        public byte[]? Bytes { get; set; } = bytes;
        public PdfConversionStatus Status { get; set; } = new(Guid.NewGuid(), "Waiting to process PDF...");
        public DateTimeOffset Expires { get; set; } = DateTimeOffset.MaxValue;
    }

    private readonly object gate = new();
    private readonly Dictionary<Guid, Job> jobs = [];
    private readonly Channel<Job> queue = Channel.CreateUnbounded<Job>();

    public async Task<IResult> Enqueue(IFormFile file, ITenantContext tenant, CancellationToken ct)
    {
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || file.Length is <=0 or > OrderDocumentImport.MaxBytes)
            return Results.BadRequest(new ApiErrorDto("Choose a nonempty PDF up to 10 MB."));
        var owner = new Owner(tenant.TenantId, tenant.UserId);
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        lock (gate)
        {
            PurgeExpired();
            if (jobs.Values.Count(j => j.Expires == DateTimeOffset.MaxValue) >= 12)
                return Results.Json(new ApiErrorDto("PDF processing is busy. Try again shortly."), statusCode:429);
            var job = new Job(owner, Path.GetFileName(file.FileName), buffer.GetBuffer());
            jobs.Add(job.Status.Id, job);
            queue.Writer.TryWrite(job);
            return Results.Accepted(value:job.Status);
        }
    }

    public IResult Status(Guid id, ITenantContext tenant, int? after)
    {
        lock (gate)
        {
            PurgeExpired();
            if (!jobs.TryGetValue(id,out var job) || job.Owner!=new Owner(tenant.TenantId,tenant.UserId))
                return Results.NotFound(new ApiErrorDto("This PDF review has expired. Upload the file again."));
            if (after is null || job.Status.Orders is not null || job.Status.Error is not null) return Results.Ok(job.Status);
            if (after<0 || after>job.Ready.Count) return Results.BadRequest();
            return Results.Ok(job.Status with {ReadyOrders=job.Ready.GetRange(after.Value,job.Ready.Count-after.Value)});
        }
    }

    public IResult Source(Guid id, ITenantContext tenant)
    {
        lock (gate)
        {
            PurgeExpired();
            if (!jobs.TryGetValue(id,out var job) || job.Owner!=new Owner(tenant.TenantId,tenant.UserId) || job.Bytes is null)
                return Results.NotFound();
            return Results.Bytes(job.Bytes,"application/pdf");
        }
    }

    private void PurgeExpired()
    {
        foreach (var id in jobs.Where(p => p.Value.Expires <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray())
            jobs.Remove(id);
    }

    protected override Task ExecuteAsync(CancellationToken ct) =>
        Task.WhenAll(Consume(ct), Consume(ct), Cleanup(ct));

    private async Task Cleanup(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(ct)) lock (gate) PurgeExpired();
    }

    private async Task Consume(CancellationToken ct)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(ct))
        {
            List<OrderDocumentDraft>? orders = null;
            string? error = null;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ConversionWorkerIdentity>().Identity = job.Owner;
                var conversion = scope.ServiceProvider.GetRequiredService<OrderDocumentConversion>();
                using var stream = new MemoryStream(job.Bytes!, writable:false);
                var file = new FormFile(stream, 0, stream.Length, "file", job.Name);
                void Progress(string message) { lock (gate) job.Status = job.Status with { Progress = message }; }
                Progress("Reading PDF...");
                var result = await conversion.Convert(file, null, ct, deferAddressLookup:true, reportProgress:Progress,
                    ordersReady:rows=>{lock(gate) job.Ready.AddRange(rows);});
                var value = (result as IValueHttpResult)?.Value;
                if (value is List<OrderDocumentDraft> drafts) orders = drafts;
                else error = (value as ApiErrorDto)?.Error ?? "PDF conversion could not finish.";
            }
            catch (Exception ex)
            {
                log.LogError(ex, "PDF conversion job {Id} failed", job.Status.Id);
                error = "PDF conversion could not finish. Please retry the upload.";
            }
            finally
            {
                lock (gate)
                {
                    job.Ready.Clear();
                    if (error is not null) job.Bytes = null;
                    job.Expires = DateTimeOffset.UtcNow.AddMinutes(30);
                    job.Status = job.Status with { Progress = error is null ? "Ready for review" : "Conversion failed", Orders = orders, Error = error };
                }
            }
        }
    }
}
