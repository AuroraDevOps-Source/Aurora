using System.Text.Json;
using Aurora.Contracts;
using Aurora.Modules.Routing.Optimization;
using Aurora.Modules.Routing.Queries.ListRoutePlans;

namespace Aurora.Api.Features.Routing;

public static class RoutingEndpoints
{
    private const long MaxUploadBytes = 25 * 1024 * 1024;

    public static void MapRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/routing").RequireAuthorization();

        group.MapGet("/route-plans", async (ListRoutePlansQuery query, CancellationToken cancellationToken) =>
            Results.Ok(await query.ExecuteAsync(cancellationToken)));

        group.MapPost("/optimizations", RunOptimization).DisableAntiforgery();
        group.MapPost("/road-route", RoadRoutingEndpoint.Calculate);
        group.MapPost("/sessions", async (StartPlanningDto input, HttpContext context, PlanningSessions sessions, Aurora.Modules.Routing.OrderWorkspace workspace, CancellationToken ct) =>
            {
                try
                {
                    if (input.WorkspaceDraftId is { } draftId) input = input with { RequestJson = await workspace.ValidateDraft(draftId, PlanningSessions.Owner(context.User), input.RequestJson, ct) };
                    return await sessions.Start(input, context.User);
                }
                catch (FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
                catch (KeyNotFoundException) { return Results.NotFound(new ApiErrorDto("Planning selection not found.")); }
            })
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(60 * 1024 * 1024));
        group.MapGet("/sessions/{id:guid}", (Guid id, HttpContext context, PlanningSessions sessions, CancellationToken ct) => sessions.Poll(id, context.User, false, ct, context.Request.Query.ContainsKey("inputs")));
        group.MapPost("/sessions/{id:guid}/stop", (Guid id, HttpContext context, PlanningSessions sessions, CancellationToken ct) => sessions.Poll(id, context.User, true, ct));
        group.MapGet("/equipment", async (Aurora.Modules.Routing.EquipmentStore store, CancellationToken ct) => Results.Ok(await store.List(ct)));
        group.MapPost("/equipment/import", async (EquipmentCatalogDto catalog, Aurora.Modules.Routing.EquipmentStore store, CancellationToken ct) =>
        {
            try { await store.AddMissing(catalog, ct); return Results.Ok(); }
            catch (FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
        });
        group.MapPut("/equipment/types", async (EquipmentTypeDto item, Aurora.Modules.Routing.EquipmentStore store, CancellationToken ct) =>
        {
            try { await store.Save(item, ct); return Results.Ok(); }
            catch (FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
        });
        group.MapPut("/equipment/units", async (EquipmentUnitDto item, Aurora.Modules.Routing.EquipmentStore store, CancellationToken ct) =>
        {
            try { await store.Save(item, ct); return Results.Ok(); }
            catch (FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
        });
    }

    /// <summary>
    /// Accepts a PTV-format JSON manifest and returns the optimized routes. The upload checks
    /// mirror the tool this was ported from: reject anything that is not a sane JSON file before
    /// spending a PTV call on it.
    /// </summary>
    private static async Task<IResult> RunOptimization(
        HttpRequest request,
        OptimizationService optimizations,
        CancellationToken cancellationToken)
    {
        if (!optimizations.IsConfigured)
        {
            return Results.Json(
                new ApiErrorDto("PTV is not configured. Set Ptv:ApiKey on the server."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new ApiErrorDto("Upload a JSON file using multipart form data."));
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new ApiErrorDto("Choose a non-empty JSON file."));
        }

        if (file.Length > MaxUploadBytes)
        {
            return Results.BadRequest(new ApiErrorDto("The JSON file must be 25 MB or smaller."));
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new ApiErrorDto("Only .json files are accepted."));
        }

        string requestJson;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream))
        {
            requestJson = await reader.ReadToEndAsync(cancellationToken);
        }

        try
        {
            using var _ = JsonDocument.Parse(requestJson);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new ApiErrorDto("The selected file is not valid JSON.", [ex.Message]));
        }

        try
        {
            string? previousResult = null;
            var mode = form["mode"].ToString();
            if (mode is not ("" or "full" or "quick"))
                return Results.BadRequest(new ApiErrorDto("Choose Quick update or Full optimization."));
            if (mode == "quick")
            {
                var previous = form.Files.GetFile("previousResult");
                if (previous is null || previous.Length == 0 || previous.Length > MaxUploadBytes)
                    return Results.BadRequest(new ApiErrorDto("Quick update needs a previous result of 25 MB or smaller."));
                await using var previousStream = previous.OpenReadStream();
                using var previousReader = new StreamReader(previousStream);
                previousResult = await previousReader.ReadToEndAsync(cancellationToken);
            }
            var result = await optimizations.OptimizeAsync(
                Path.GetFileName(file.FileName), requestJson, cancellationToken, previousResult);

            return Results.Ok(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException or ArgumentException)
        {
            return Results.BadRequest(new ApiErrorDto("The routing file contains invalid or conflicting settings.", [ex.Message]));
        }
        catch (TaskCanceledException ex)
        {
            return Results.Json(new ApiErrorDto("A PTV request timed out.", [ex.Message]),
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException ex)
        {
            return Results.Json(new ApiErrorDto("The server could not reach PTV.", [ex.Message]),
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
