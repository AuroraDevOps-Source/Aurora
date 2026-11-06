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
    IOptions<FirebirdRoutingOptions> nativeOptions)
{
    public async Task<IResult> Convert(IFormFile file, string? previewInstructions, CancellationToken ct, string? previewGlobal = null)
    {
        if (file.Length is <= 0 or > OrderDocumentImport.MaxBytes)
            return Results.BadRequest(new ApiErrorDto("Choose a nonempty document up to 10 MB."));
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".csv" or ".txt" or ".tsv" or ".xlsx" or ".docx" or ".tif" or ".tiff" or ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp"))
            return Results.BadRequest(new ApiErrorDto("Unsupported document type. Choose PDF, CSV, text, XLSX, DOCX, or an image."));

        List<OrderDocumentDraft>? drafts = null;
        if (extension == ".xlsx")
        {
            try
            {
                await using var uploaded = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await uploaded.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                drafts = CrownOrderReport.Read(buffer, TimeZoneInfo.FindSystemTimeZoneById(config["FirebirdRouting:TimeZoneId"] ?? "America/New_York"));
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or System.Xml.XmlException)
            {
                return Results.BadRequest(new ApiErrorDto("Could not read the spreadsheet. " + ex.Message));
            }
        }

        var preview = previewInstructions is not null;
        if (preview && drafts is not null)
            return Results.BadRequest(new ApiErrorDto("Aurora reads Crown order reports directly. Choose another sample to test extraction instructions."));

        // Tenant and Hub credentials always come from the authenticated request context.
        var settings = config.GetSection($"DocumentConversion:Tenants:{tenant.TenantId:D}");
        Uri.TryCreate(settings["BaseUrl"], UriKind.Absolute, out var baseUrl);
        var credential = settings["ServiceCredential"];
        if (drafts is null && (baseUrl?.Scheme != "https" || string.IsNullOrWhiteSpace(credential)))
            return Results.Json(new ApiErrorDto("Document conversion is not configured for your company. Contact your administrator."), statusCode: 503);

        Guid? batch = null;
        try
        {
            if (drafts is null)
            {
                var saved = await ReadSavedInstructions(ct);
                if (saved is null || (previewGlobal is not null && (!preview || !saved.CanEditGlobal))) return Results.Forbid();
                var global = DocumentExtractionPrompt.ValidateInstructions(previewGlobal ?? saved.Global, TenantSettingsDto.MaxGlobalInstructionsLength);
                var company = DocumentExtractionPrompt.ValidateInstructions(previewInstructions ?? saved.Company);
                var prompt = DocumentExtractionPrompt.Build(global, company);
                if (!preview) batch = await history.Start(file.FileName, ct);
                try
                {
                    var extraction = await Extract(file, extension, baseUrl!, credential!, prompt, ct);
                    if (preview && extraction.ValueKind == JsonValueKind.Array && extraction.GetArrayLength() == 0)
                        return Results.Ok(Array.Empty<DocumentExtractionSample>());
                    drafts = OrderDocumentImport.Parse(extraction,
                        TimeZoneInfo.FindSystemTimeZoneById(nativeOptions.Value.TimeZoneId), settings.GetValue<bool>("RequirePrintedLykesTag"));
                    if (preview)
                        return Results.Ok(drafts.Select((draft, i) => new DocumentExtractionSample(draft.Order.Id, draft.Order.Customer, extraction[i])).ToArray());
                }
                catch (Exception ex) when (ex is JsonException or FormatException)
                {
                    return Results.BadRequest(new ApiErrorDto("The converted document did not contain usable orders. " + ex.Message));
                }
            }
            batch ??= await history.Start(file.FileName, ct);
            await native.ResolveDocumentAddresses(drafts, ct);
            await history.Converted(batch.Value, drafts.Count, ct);
            foreach (var draft in drafts) draft.ImportId = batch;
            batch = null;
            return Results.Ok(drafts);
        }
        catch (FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
        catch (HttpRequestException ex) when (ex.StatusCode is {} status)
        {
            return Results.Json(new ApiErrorDto($"Integration Hub could not convert the document (HTTP {(int)status}). No orders were created."), statusCode: 502);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return Results.Json(new ApiErrorDto("Document conversion timed out or is unavailable. No orders were created. Try again."), statusCode: 503);
        }
        finally
        {
            if (batch.HasValue) await history.Failed(batch.Value);
        }
    }

    private sealed record Instructions(string Global, string Company, bool CanEditGlobal);

    private async Task<Instructions?> ReadSavedInstructions(CancellationToken ct)
    {
        await using var db = await connections.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<Instructions>(new CommandDefinition(
            "SELECT instructions AS Global, public.aurora_document_extraction_instructions() AS Company, public.can_edit_global_document_settings() AS CanEditGlobal FROM public.global_document_settings", cancellationToken: ct));
    }

    private async Task<JsonElement> Extract(IFormFile file, string extension, Uri baseUrl, string credential, string prompt, CancellationToken ct)
    {
        using var multipart = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(content, "file", "document" + extension);
        multipart.Add(new StringContent(prompt), "prompt");
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.ToString().TrimEnd('/') + "/api/document-convert/service") { Content = multipart };
        request.Headers.Authorization = new AuthenticationHeaderValue("DocumentService", credential);
        using var client = clients.CreateClient("OrderDocumentConversion");
        client.Timeout = TimeSpan.FromMinutes(5);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return OrderDocumentImport.ReadExtraction(await response.Content.ReadAsStringAsync(ct));
    }
}
