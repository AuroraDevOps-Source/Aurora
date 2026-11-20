using System.Net.Http.Headers;
using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.Nova;
using Dapper;
using Microsoft.Extensions.Options;

namespace Aurora.Api.Features.Routing;

public sealed class OrderDocumentConversion(ITenantContext tenant, IConfiguration config, IHttpClientFactory clients,
    NpgsqlConnectionFactory connections, OrderImportHistoryStore history, FirebirdRoutingSource native,
    IOptions<FirebirdRoutingOptions> nativeOptions, ILogger<OrderDocumentConversion>? logger = null)
{
    public async Task<IResult> Convert(IFormFile file, string? previewInstructions, CancellationToken ct, string? previewGlobal = null, bool deferAddressLookup = false, Action<string>? reportProgress = null,List<DocumentFormRule>? previewRules=null, Action<List<OrderDocumentDraft>>? ordersReady=null)
    {
        if (file.Length is <= 0 or > OrderDocumentImport.MaxBytes)
            return Results.BadRequest(new ApiErrorDto("Choose a nonempty document up to 10 MB."));
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".csv" or ".txt" or ".tsv" or ".xlsx" or ".docx" or ".tif" or ".tiff" or ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp"))
            return Results.BadRequest(new ApiErrorDto("Unsupported document type. Choose PDF, CSV, text, XLSX, DOCX, or an image."));

        Guid? batch = previewInstructions is null ? await history.Start(file.FileName,ct) : null;
        try
        {
            var result = await Process(file, extension, previewInstructions, previewGlobal, deferAddressLookup, reportProgress, batch, previewRules, ordersReady, ct);
            if (batch is {} id && result is IStatusCodeHttpResult { StatusCode: >=400 } or Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult)
            {
                var reason = (result as IValueHttpResult)?.Value is ApiErrorDto error ? error.Error : "Access to conversion settings was denied.";
                await history.Failed(id,reason);
            }
            return result;
        }
        catch (Exception ex)
        {
            if (batch is {} id)
            {
                logger?.LogError(ex,"Document conversion failed for import {ImportId}",id);
                await history.Failed(id,ex is OperationCanceledException ? "Conversion was interrupted before it finished." :
                    $"Unexpected document conversion error. Support reference: {id}.");
            }
            throw;
        }
    }

    private async Task<IResult> Process(IFormFile file,string extension,string? previewInstructions,string? previewGlobal,
        bool deferAddressLookup,Action<string>? reportProgress,Guid? batch,List<DocumentFormRule>? previewRules,Action<List<OrderDocumentDraft>>? ordersReady,CancellationToken ct)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        string route="Crown";
        List<OrderDocumentDraft>? drafts = null;
        if (extension == ".xlsx")
        {
            try
            {
                await using var uploaded = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await uploaded.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                var zone = TimeZoneInfo.FindSystemTimeZoneById(config["FirebirdRouting:TimeZoneId"] ?? "America/New_York");
                drafts = CrownOrderReport.Read(buffer, zone);
                if (drafts is null)
                {
                    buffer.Position = 0;
                    drafts = LykesDailyReport.Read(buffer, zone);
                    if (drafts is not null) route = "Lykes daily report";
                }
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or System.Xml.XmlException)
            {
                return Results.BadRequest(new ApiErrorDto("Could not read the spreadsheet. " + ex.Message));
            }
        }

        bool publishedIncrementally=false;
        var preview = previewInstructions is not null;
        if (preview && drafts is not null && route == "Lykes daily report")
            return Results.Ok(drafts.Select(d => new DocumentExtractionSample(d.Order.Id, d.Order.Customer, JsonSerializer.SerializeToElement(d))).ToArray());
        if (preview && drafts is not null)
            return Results.BadRequest(new ApiErrorDto("Aurora reads Crown order reports directly. Choose another sample to test extraction instructions."));

        // Tenant and Hub credentials always come from the authenticated request context.
        var settings = config.GetSection($"DocumentConversion:Tenants:{tenant.TenantId:D}");
        Uri.TryCreate(settings["BaseUrl"], UriKind.Absolute, out var baseUrl);
        var credential = settings["ServiceCredential"];
        if (drafts is null && (baseUrl?.Scheme != "https" || string.IsNullOrWhiteSpace(credential)))
            return Results.Json(new ApiErrorDto("Document conversion is not configured for your company. Contact your administrator."), statusCode: 503);

        try
        {
            if (drafts is null)
            {
                var saved = await ReadSavedInstructions(ct);
                if (saved is null || (previewGlobal is not null && (!preview || !saved.CanEditGlobal))) return Results.Forbid();
                var global = DocumentExtractionPrompt.ValidateInstructions(previewGlobal ?? saved.Global, TenantSettingsDto.MaxGlobalInstructionsLength);
                var company = DocumentExtractionPrompt.ValidateInstructions(previewInstructions ?? saved.Company);
                var formRules=previewRules ?? JsonSerializer.Deserialize<List<DocumentFormRule>>(saved.FormRules) ?? [];
                if (formRules.Count==0 && !preview && extension==".pdf" && !settings.GetValue<bool>("RequirePrintedLykesTag") && AtlasDeliveryTickets.Supports(company, global))
                {
                    await using var pdf = file.OpenReadStream();
                    drafts = AtlasDeliveryTickets.Read(pdf, ct);
                    route = "Atlas";
                }
                if (drafts is null)
                {
                    route = "Hub";
                    var prompt = DocumentExtractionPrompt.Build(global, company);

                    try
                    {
                        var hubTimer = System.Diagnostics.Stopwatch.StartNew();
                        var packet=extension==".pdf" ? await ReadPacket(file,ct) : [];
                        var zone=TimeZoneInfo.FindSystemTimeZoneById(nativeOptions.Value.TimeZoneId);
                        var strictTags=settings.GetValue<bool>("RequirePrintedLykesTag");
                        var mappings=new DocumentMappingStore(connections,tenant);
                        async Task<List<OrderDocumentDraft>> Prepare(JsonElement data)
                        {
                            var rows=OrderDocumentImport.Parse(data,zone,strictTags,maxOrders:reportProgress is null ? 100 : int.MaxValue,
                                pdfRules:extension==".pdf" ? formRules : null);
                            foreach (var draft in rows.Where(d=>d.ExtractionError is null))
                            {
                                if(extension is not (".pdf" or ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".webp" or ".gif" or ".bmp"))
                                    draft.LogoCompany=null;
                                DocumentMappings.PrepareBilling(draft,global);
                                if(packet.Count>0) ApplyPacketMapping(draft,packet.Single(m=>m.FirstPage==draft.SourcePages[0]));
                            }
                            if (!preview) await mappings.Apply(rows,strictTags,ct);
                            return rows;
                        }

                        var streamed=new List<OrderDocumentDraft>();
                        async Task PublishShipment(JsonElement[] shipment)
                        {
                            var rows=await Prepare(JsonSerializer.SerializeToElement(shipment));
                            // The two extraction workers may finish out of page order. Assign stable review IDs once.
                            lock(streamed)
                            {
                                foreach(var row in rows)
                                {
                                    Identify(row,batch,streamed.Count+1,extension);
                                    streamed.Add(row);
                                }
                                ordersReady!(rows);
                            }
                        }

                        bool streaming=ordersReady is not null && !preview && extension==".pdf" && deferAddressLookup;
                        var extraction = extension==".pdf"
                            ? await PdfPacketExtraction.Read(file, prompt, formRules, (part, instructions, indexing) => Extract(part, extension, baseUrl!, credential!, instructions, ct, indexing), reportProgress, ct, packet,
                                streaming ? PublishShipment : null)
                            : await Extract(file, extension, baseUrl!, credential!, prompt, ct);
                        logger?.LogInformation("Integration Hub extraction for {Extension} in {ElapsedMs} ms",extension,hubTimer.ElapsedMilliseconds);
                        if (streaming) { drafts=streamed.OrderBy(d=>d.SourcePages[0]).ToList(); publishedIncrementally=true; }
                        else if ((preview || reportProgress is not null) && extraction.ValueKind == JsonValueKind.Array && extraction.GetArrayLength() == 0)
                            drafts=[];
                        else drafts=await Prepare(extraction);
                        if (preview)
                            return Results.Ok(drafts.Select((draft, i) =>
                            {
                                var fields=extraction[i].EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value,StringComparer.OrdinalIgnoreCase);
                                if(draft.LogoCompany is not null || !string.IsNullOrWhiteSpace(draft.BillingEvidence))
                                {
                                    var order=fields.TryGetValue("Order",out var orderFields) && orderFields.ValueKind==JsonValueKind.Object
                                        ? orderFields.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value,StringComparer.OrdinalIgnoreCase)
                                        : new Dictionary<string,JsonElement>(StringComparer.OrdinalIgnoreCase);
                                    foreach(var key in NovaImportAddress.Parties.Single(p=>p.Part=="header").Properties)
                                        order[key]=JsonSerializer.SerializeToElement(typeof(TmsOrder).GetProperty(key)!.GetValue(draft.Order));
                                    fields["Order"]=JsonSerializer.SerializeToElement(order);
                                    fields["BillToAccountId"]=JsonSerializer.SerializeToElement(draft.BillToAccountId);
                                    fields["BillingEvidence"]=JsonSerializer.SerializeToElement(draft.BillingEvidence);
                                }
                                fields["BillOfLading"]=JsonSerializer.SerializeToElement(draft.BillOfLading);
                                fields["MappingQuestions"]=JsonSerializer.SerializeToElement(draft.MappingQuestions);
                                return new DocumentExtractionSample(draft.Order.Id,draft.Order.Customer,JsonSerializer.SerializeToElement(fields));
                            }).ToArray());
                    }
                    catch (Exception ex) when (ex is JsonException or FormatException)
                    {
                        return Results.BadRequest(new ApiErrorDto("The converted document did not contain usable orders. " + ex.Message));
                    }
                }
            }
            logger?.LogInformation("Document extraction {Route} {Extension} {Bytes} bytes {Orders} orders in {ElapsedMs} ms",route,extension,file.Length,drafts.Count,timer.ElapsedMilliseconds);

            if (!deferAddressLookup) await native.ResolveDocumentAddresses(drafts, ct);
            if (!publishedIncrementally)
                for (var i=0;i<drafts.Count;i++) Identify(drafts[i],batch,i+1,extension);
            if(batch is {} id)
            {
                var failures=drafts.Where(d=>d.ExtractionError is not null).Select(d=>new ImportFailure(
                    d.ImportRowId,"PDF pages "+string.Join(", ",d.SourcePages),d.ExtractionError!)).ToArray();
                await history.Converted(id,drafts.Count,ct,failures);
            }
            return Results.Ok(drafts);
        }
        catch (FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            logger?.LogWarning("Integration Hub rejected document conversion authentication for tenant {TenantId} (HTTP {StatusCode})", tenant.TenantId, (int)ex.StatusCode.Value);
            return Results.Json(new ApiErrorDto("Document conversion service authentication failed. Contact your administrator to check your company's Integration Hub service credential. No orders were created."), statusCode: 502);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is {} status)
        {
            return Results.Json(new ApiErrorDto($"Integration Hub could not convert the document (HTTP {(int)status}). No orders were created."), statusCode: 502);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return Results.Json(new ApiErrorDto("Document conversion timed out or is unavailable. No orders were created. Try again."), statusCode: 503);
        }
    }

    private static void Identify(OrderDocumentDraft draft,Guid? batch,int rowNumber,string extension)
    {
        draft.IsSpreadsheetImport = extension is ".xlsx" or ".csv" or ".tsv";
        draft.ImportId=batch;
        draft.ImportRowNumber=rowNumber;
        draft.ImportRowId=Guid.NewGuid();
        if(draft.ExtractionError is null) DocumentMappings.RequireBol(draft);
        else draft.MappingQuestions=[];
    }

    private async Task<List<PacketMapping>> ReadPacket(IFormFile file,CancellationToken ct)
    {
        await using var stream=file.OpenReadStream();
        var hash=System.Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream,ct));
        await using var db=await connections.OpenConnectionAsync(ct);
        return (await db.QueryAsync<PacketMapping>(new CommandDefinition("""
            SELECT first_page AS FirstPage,last_page AS LastPage,bol,payer,issuer,form,
                extraction_last_page AS ExtractionLastPage,nova_assigns_pro AS NovaAssignsPro,handling_terminal AS HandlingTerminal FROM document_packet_mapping
            WHERE tenant_id=@TenantId AND file_sha256=@hash ORDER BY first_page
            """,new{tenant.TenantId,hash},cancellationToken:ct))).ToList();
    }

    private static void ApplyPacketMapping(OrderDocumentDraft draft,PacketMapping mapping)
    {
        draft.UseReviewedPacketBol=true;
        draft.BillOfLading=draft.Order.Reference=mapping.Bol;
        if (mapping.HandlingTerminal is {} terminal)
            draft.Order.TerminalCode = draft.TerminalCode = terminal;
        var question=draft.MappingQuestions.FirstOrDefault(q=>q.Field=="BillTo");
        if(!string.Equals(draft.Order.BillTo?.Trim(),mapping.Payer,StringComparison.OrdinalIgnoreCase) &&
            (mapping.Issuer is not null || question is not null || string.IsNullOrWhiteSpace(draft.Order.BillTo)))
        {
            DocumentMappings.Answer(draft,question ?? new(){Field="BillTo"},new("Packet selection",mapping.Payer));
            draft.LogoCompany=null;
            draft.BillToContact=draft.BillToPhone=draft.BillToEmail=null;
            draft.BillingEvidence="Bill To selected for this packet: "+mapping.Payer;
        }
        draft.MappingQuestions=draft.MappingQuestions.Where(q=>q.Field is not ("BillTo" or "BOL")).ToArray();
    }

    private sealed record Instructions(string Global, string Company, string FormRules, bool CanEditGlobal);

    private async Task<Instructions?> ReadSavedInstructions(CancellationToken ct)
    {
        await using var db = await connections.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<Instructions>(new CommandDefinition(
            "SELECT instructions AS Global, public.aurora_document_extraction_instructions() AS Company, public.aurora_document_form_rules()::text AS FormRules, public.can_edit_global_document_settings() AS CanEditGlobal FROM public.global_document_settings", cancellationToken: ct));
    }

    private async Task<JsonElement> Extract(IFormFile file, string extension, Uri baseUrl, string credential, string prompt, CancellationToken ct, bool indexing=false)
    {
        using var client = clients.CreateClient("OrderDocumentConversion");
        client.Timeout = TimeSpan.FromMinutes(5);
        string body;
        for(int attempt=0;;attempt++)
        {
            using var multipart = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();
            var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(content, "file", "document" + extension);
            multipart.Add(new StringContent(prompt), "prompt");
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.ToString().TrimEnd('/') + "/api/document-convert/service") { Content = multipart };
            request.Headers.Authorization = new AuthenticationHeaderValue("DocumentService", credential);
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode==System.Net.HttpStatusCode.TooManyRequests && attempt<2)
            {
                var retry=response.Headers.RetryAfter;
                var delay=retry?.Delta ?? (retry?.Date-DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(20*(attempt+1));
                if (delay<=TimeSpan.FromMinutes(2))
                {
                    await Task.Delay(delay>TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1),ct);
                    continue;
                }
            }
            response.EnsureSuccessStatusCode();
            body=await response.Content.ReadAsStringAsync(ct);
            break;
        }
        try { return OrderDocumentImport.ReadExtraction(body); }
        catch(JsonException) when(indexing)
        {
            // Some model replies wrap the JSON index in prose. Validate its schema and page sequence in the caller.
            var modelReply=body.TrimStart().StartsWith('"') ? JsonSerializer.Deserialize<string>(body) ?? "" : body;
            var first=modelReply.IndexOf('[');
            var last=modelReply.LastIndexOf(']');
            if(first<0 || last<=first)throw;
            return JsonSerializer.Deserialize<JsonElement>(modelReply[first..(last+1)]);
        }
    }
}
