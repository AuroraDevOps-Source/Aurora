using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Tenancy;
using Aurora.Infrastructure.Data;
using Aurora.Api.Features.Auth;
using Aurora.Modules.Routing;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Features.Routing;

public static class OrderDocumentImport
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public static void MapOrderDocumentImport(this RouteGroupBuilder group)
    {
        group.MapGet("/orders/import/history", async (OrderImportHistoryStore history,CancellationToken ct)=>Results.Ok(await history.List(ct)));
        group.MapGet("/orders/import/history/{id:guid}/failures", async (Guid id, OrderImportHistoryStore history,CancellationToken ct)=>Results.Ok(await history.Failures(id,ct)));
        group.MapPut("/orders/import/history/{id:guid}/counts", async (Guid id, ImportReviewCounts counts,
            OrderImportHistoryStore history, CancellationToken ct) =>
            await history.SaveReviewCounts(id, counts, ct) ? Results.NoContent() : Results.BadRequest());
        group.MapPost("/orders/import/check", (string[] ids, TmsStore store, NpgsqlConnectionFactory factory,
            ITenantContext tenant, NovaUserConnections connections, ILogger<OrderDocumentConversion> log, CancellationToken ct) => ImportGuard(async () =>
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var existing=await OrderImportChecks.Existing(ids,store,factory,tenant,connections,ct);
            log.LogInformation("Import duplicate check for {Count} identifiers in {ElapsedMs} ms",ids.Length,timer.ElapsedMilliseconds);
            return Results.Ok(existing);
        }));
        group.MapPost("/orders/import", (OrderDocumentDraft draft, TmsStore store, TerminalStore terminals,
            NpgsqlConnectionFactory factory, ITenantContext tenant, NovaUserConnections connections, OrderImportHistoryStore history, CancellationToken ct) => history.TrackCreation(draft, () => ImportGuard(async () =>
        {
            if(draft.ExtractionError is not null)throw new FormatException("Extract the failed shipment again before creating its order.");
            if (string.IsNullOrWhiteSpace(draft.BillOfLading)) throw new FormatException("Enter a BOL number before creating this order.");
            if (draft.MappingQuestions is {Length:>0}) throw new FormatException("Answer the document mapping questions before creating this order.");
            await history.Validate(draft.ImportId,ct);
            draft.Normalize();
            var order = draft.Order;
            var selected = (await terminals.List(ct)).SingleOrDefault(t => t.Id == order.TerminalId);
            ResetIdentity(order);
            order.TerminalCode = selected?.Code;
            order.Notes = draft.NotesWithReferences(includeLines:true);
            if ((await OrderImportChecks.Existing([order.Id], store, factory, tenant, connections, ct)).Length > 0)
                return Results.Conflict(DuplicateImportPro.For(order.Id));
            try { await store.SaveOrder(order, true, ct, documentImport:true); }
            catch(DuplicateProException){return Results.Conflict(DuplicateImportPro.For(order.Id));}
            catch(Npgsql.PostgresException ex) when(ex.SqlState=="23505")
            {
                if((await store.ExistingOrderNumbers([order.Id],ct)).Length>0)return Results.Conflict(DuplicateImportPro.For(order.Id));
                throw;
            }
            await history.RecordCreated(draft.ImportId,order.Id);
            return Results.Ok(order);
        })));
        group.MapPost("/orders/import/mapping-rule", (DocumentMappingRule rule, DocumentMappingStore store, CancellationToken ct)=>store.Save(rule,ct));
        group.MapPost("/orders/import/addresses", async (OrderDocumentDraft[] drafts, Aurora.Integrations.Nova.FirebirdRoutingSource native, ILogger<OrderDocumentConversion> log, CancellationToken ct)=>
        {
            if(drafts.Length is 0 or >100 || drafts.Any(d=>d?.Order is null)) return Results.BadRequest();
            var timer=System.Diagnostics.Stopwatch.StartNew();
            await native.ResolveDocumentAddresses(drafts,ct);
            log.LogInformation("Import address lookup for {Orders} orders in {ElapsedMs} ms",drafts.Length,timer.ElapsedMilliseconds);
            return Results.Ok(drafts);
        });
        group.MapPost("/orders/convert-document/jobs", (IFormFile file, PdfConversionJobs jobs, ITenantContext tenant, CancellationToken ct) =>
            jobs.Enqueue(file, tenant, ct)).WithDocumentUploadLimits();
        group.MapGet("/orders/convert-document/jobs/{id:guid}/source", (Guid id, PdfConversionJobs jobs, ITenantContext tenant) => jobs.Source(id,tenant));
        group.MapGet("/orders/convert-document/jobs/{id:guid}", (Guid id, int? after, PdfConversionJobs jobs, ITenantContext tenant) =>
            jobs.Status(id, tenant, after));
        group.MapPost("/orders/convert-document", (IFormFile file, bool? earlyReview, OrderDocumentConversion conversion, CancellationToken ct) =>
            conversion.Convert(file, previewInstructions: null, ct, deferAddressLookup:earlyReview==true)).WithDocumentUploadLimits();
    }

    internal static RouteHandlerBuilder WithDocumentUploadLimits(this RouteHandlerBuilder endpoint) => endpoint
        .WithMetadata(new RequestSizeLimitAttribute(MaxBytes + 131072))
        .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxBytes + 131072 })
        .DisableAntiforgery(); // The API uses bearer authentication, not ambient cookies.

    public static List<OrderDocumentDraft> Parse(string json,TimeZoneInfo? zone=null,bool requirePrintedLykesTag=false)
        => Parse(ReadExtraction(json), zone, requirePrintedLykesTag);

    internal static JsonElement ReadExtraction(string json)
    {
        var outer = JsonSerializer.Deserialize<JsonElement>(json);
        if (outer.ValueKind != JsonValueKind.String) return outer;
        json = outer.GetString()!.Trim();
        if (json.StartsWith("```"))
        {
            var start = json.IndexOf('\n');
            var end = json.LastIndexOf("```", StringComparison.Ordinal);
            if (start < 0 || end <= start) throw new FormatException("Invalid JSON response.");
            json = json[(start + 1)..end];
        }
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    internal static List<OrderDocumentDraft> Parse(JsonElement extraction, TimeZoneInfo? zone, bool requirePrintedLykesTag, int maxOrders=100, IReadOnlyList<DocumentFormRule>? pdfRules=null)
    {
        var orders = extraction.Deserialize<List<OrderDocumentDraft>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new FormatException("No orders returned.");
        if (orders.Count < 1 || orders.Count > maxOrders || orders.Any(o => o is null)) throw new FormatException("Import 1–100 orders at a time.");
        foreach (var draft in orders)
        {
            if (draft.Order is null) throw new FormatException("Order details are required.");
            draft.NativeStatusId=null;
            draft.ImportId=null; // Never trust a batch identity supplied by document extraction.
            draft.AddressLookupMessages=[];
            var order = draft.Order;
            var terminal=(string.IsNullOrWhiteSpace(draft.TerminalCode)?order.TerminalCode:draft.TerminalCode)?.Trim().ToUpperInvariant();
            ResetIdentity(order);
            order.TerminalId = null;
            order.TerminalCode = null;
            order.Reference = draft.BillOfLading?.Trim() ?? "";
            if (pdfRules?.Any(r=>r.UsePrintedPro && r.Matches(draft.MappingSender,draft.MappingForm,draft.MappingAccount))==true)
            {
                // Only honor the labeled identifier on this issuer's form. A bill number is never a PRO fallback.
                var pros=(draft.PrintedIdentifiers ?? []).Where(c=>c is not null && !string.IsNullOrWhiteSpace(c.Value) &&
                    System.Text.RegularExpressions.Regex.IsMatch(c.Label??"", @"^\s*PRO(?:\s*(?:#|NO\.?|NUMBER|NUM\.?))?\s*:?\s*$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                    .Select(c=>c.Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                draft.TagNumber="";
                draft.PrintedLykesTag="";
                order.Id=pros.Length==1 ? pros[0] : "";
            }
            else if ((requirePrintedLykesTag || string.Equals(draft.DocumentType,"Lykes",StringComparison.OrdinalIgnoreCase))
                && string.IsNullOrWhiteSpace(draft.PrintedLykesTag))
            {
                foreach (var identifier in new[]{draft.TagNumber,order.Id}.Where(v=>!string.IsNullOrWhiteSpace(v)).Distinct())
                    if (!(order.Notes??"").Contains(identifier,StringComparison.OrdinalIgnoreCase))
                        order.Notes=(order.Notes+$"\nDocument identifier: {identifier}").Trim();
                draft.TagNumber="";
                order.Id="";
            }
            if(!string.IsNullOrWhiteSpace(draft.PrintedLykesTag))draft.TagNumber=draft.PrintedLykesTag;
            if (!string.IsNullOrWhiteSpace(draft.TagNumber) || !string.IsNullOrWhiteSpace(order.Id)) draft.Normalize();
            // AIT's /X delivery suffix is not part of the Nova terminal code.
            // Keep the printed value in the notes while selecting its base terminal.
            if(string.Equals(draft.DocumentType,"AIT",StringComparison.OrdinalIgnoreCase) && terminal is not null)
            {
                var aitTerminal=System.Text.RegularExpressions.Regex.Match(terminal,@"^(?<code>[A-Z]{3})\s*/\s*X$");
                if(aitTerminal.Success)
                {
                    order.Notes=(order.Notes+$"\nPrinted delivery terminal: {terminal}").Trim();
                    terminal=aitTerminal.Groups["code"].Value;
                }
            }
            // Forward Air delivery forms identify the servicing station as Destination.
            // An explicit terminal or a Lykes Tag prefix still takes precedence.
            if(string.IsNullOrWhiteSpace(terminal) && string.IsNullOrWhiteSpace(order.TerminalCode) &&
                string.Equals(draft.DocumentType,"ForwardAir",StringComparison.OrdinalIgnoreCase) &&
                string.Equals(order.WorkType,"Delivery",StringComparison.OrdinalIgnoreCase))
                terminal=draft.DestinationTerminalCode?.Trim().ToUpperInvariant();
            if(!string.IsNullOrWhiteSpace(terminal) && System.Text.RegularExpressions.Regex.IsMatch(terminal,@"^[A-Z0-9]{2,12}$"))order.TerminalCode=terminal;
            draft.LocalScheduledAt??=draft.LocalAppointmentFrom;
            draft.ApplyLocalSchedule(zone??TimeZoneInfo.Utc);
            draft.MappingDecisions=[];
            draft.EditedAddressParts=[];
            draft.MappingQuestions = (draft.MappingQuestions ?? []).Where(q => q is not null && q.Field is ("BOL" or "BillTo"))
                .DistinctBy(q=>q.Field).ToArray();
            foreach (var question in draft.MappingQuestions)
                question.Candidates = (question.Candidates ?? []).Where(c=>c is not null && !string.IsNullOrWhiteSpace(c.Label) && !string.IsNullOrWhiteSpace(c.Value))
                    .Take(12).ToArray();
            DocumentMappings.RequireBol(draft, extracted:true);
            draft.ApplyPrintedAccount();
            draft.SeparatePartyAddresses();
            draft.ApplyFreightTotals();
        }
        return orders;
    }

    static void ResetIdentity(TmsOrder o)
    {
        o.SourceId = Guid.Empty; o.NativeOrderId = null;
        o.CustomerId = null; o.CustomerCode = null; o.ManifestId = null; o.ManifestNumber = null;
        o.PickupNodeId = null; o.DeliverToNodeId = null; o.OptimizationDraftId = null;
        o.Revision = 0; o.StopNumber = null; o.Status = WorkspaceOrderStatus.Ready;
        o.UseConsigneeAddress = true;
    }

    static async Task<IResult> ImportGuard(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (FreightConflictException ex) { return Results.Conflict(new ApiErrorDto(ex.Message)); }
        catch (FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        { return Results.Conflict(new ApiErrorDto("This PRO already exists. No order was created.")); }
        catch (Exception ex) when (ex is FbException or TimeoutException or System.Security.Cryptography.CryptographicException)
        { return Results.Json(new ApiErrorDto("Could not check the connected Nova database for existing PROs. No order was created. Check the connection and retry."), statusCode: 503); }
    }
}
