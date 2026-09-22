using Aurora.Contracts;
using Microsoft.Extensions.Options;

namespace Aurora.Modules.Routing.Optimization;

/// <summary>
/// Runs a PTV optimization and turns its raw response into the shape the Route Optimization
/// screen renders. Ported from the PTV tester: the parsing in RunSummary / RouteDetailExtractor /
/// ManifestReportExtractor is what makes PTV's output legible, and is kept intact deliberately.
/// </summary>
public sealed class OptimizationService(IOptions<PtvSettings> settings)
{
    private readonly PtvSettings _settings = settings.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public async Task<OptimizationResultDto> OptimizeAsync(
        string fileName,
        string requestJson,
        CancellationToken cancellationToken,
        string? previousResult = null)
    {
        // A root-level reporting sidecar carries display fields PTV does not accept. Keep the
        // uploaded JSON for report enrichment and send only the canonical request to PTV.
        requestJson = RoutingInput.Prepare(requestJson);
        if (previousResult is not null) requestJson = QuickUpdate.Prepare(requestJson, previousResult);
        var ptvRequestJson = ManifestReportExtractor.RemoveReportingSidecar(requestJson);

        var log = new List<string>();
        if (previousResult is not null)
            log.Add($"Quick update: {RoutingInput.Items(RoutingInput.Parse(requestJson)["routes"]).Count()} previous routes seeded; {QuickUpdate.CalculationSeconds}s calculation budget.");
        using var client = new PtvClient(_settings);
        var run = await client.RunAsync(ptvRequestJson, line => log.Add(line), cancellationToken);

        RunSummaryDto? summary = null;
        IReadOnlyList<RouteSummaryDto> routeDtos = [];
        IReadOnlyList<DroppedOrderDto> droppedDtos = [];
        IReadOnlyList<string> notes = [];
        IReadOnlyList<string> requestFacts = [];
        IReadOnlyList<string> stopAudit = [];
        var optimizedAt = DateTimeOffset.UtcNow;
        var terminal = "Not provided";
        string? summaryError = null;
        string? detailWarning = null;

        try
        {
            var parsed = RunSummary.Parse(run.Response.Body, ptvRequestJson, 1, $"PTV · {fileName}");
            var detail = RouteDetailExtractor.ExtractPtv(run.Response.Body, requestJson);
            var manifest = ManifestReportExtractor.ExtractPtv(run.Response.Body, requestJson);
            optimizedAt = manifest.OptimizedAt;
            terminal = manifest.Terminal;

            var detailsByVehicle = detail.Routes
                .GroupBy(route => route.Vehicle, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            summary = new RunSummaryDto(
                parsed.Record.Status, parsed.Record.RoutesUsed, parsed.Record.VehiclesOffered,
                parsed.Record.Scheduled, parsed.Record.Unscheduled, parsed.Record.Km,
                parsed.Record.DriveSeconds, parsed.Record.Cost, parsed.Record.Violations,
                parsed.Record.Orders, parsed.Record.Locations, parsed.Record.CostPerOrder,
                parsed.Record.KmPerOrder, parsed.Record.OrdersPerRoute, parsed.Record.CapacityLabel);

            routeDtos = parsed.Routes.Select(route =>
            {
                manifest.Routes.TryGetValue(route.Vehicle, out var manifestRoute);
                return new RouteSummaryDto(
                    route.Vehicle, route.Orders, route.Stops, route.Km, route.DriveSeconds,
                    route.Cost, route.LoadUtil, route.TimeUtil,
                    terminal,
                    manifestRoute?.BudgetedSeconds,
                    manifestRoute?.EstimatedSeconds ?? route.DriveSeconds,
                    manifestRoute?.Stops ?? route.Stops,
                    manifestRoute?.Units,
                    manifestRoute?.Weight,
                    manifestRoute?.Cubes,
                    manifestRoute?.Pros.Select(pro => new ManifestProDto(
                        pro.ProNumber, pro.StopNumber, pro.ShipToName, pro.Address1, pro.City,
                        pro.State, pro.PostalCode, pro.Units, pro.Weight, pro.Cubes, pro.BillType,
                        pro.AllottedStopSeconds, pro.Latitude, pro.Longitude, pro.Appointment, pro.Arrival, pro.Departure)).ToList() ?? [],
                    detailsByVehicle.TryGetValue(route.Vehicle, out var detailRoute)
                        ? detailRoute.Stops.Select(stop => new RouteStopDto(
                            stop.LocationId, stop.Latitude, stop.Longitude, stop.Arrival, stop.Departure,
                            stop.IsDepot, stop.OrderIds)).ToList()
                        : [],
                    manifestRoute?.Departure,
                    manifestRoute?.Return);
            }).ToList();

            droppedDtos = detail.Dropped.Select(order =>
            {
                manifest.Dropped.TryGetValue(order.OrderId, out var pro);
                return new DroppedOrderDto(
                    order.OrderId,
                    pro?.Latitude ?? order.Latitude,
                    pro?.Longitude ?? order.Longitude,
                    pro?.Reason ?? order.Reason,
                    pro?.ShipToName ?? "Not provided",
                    pro?.Address1 ?? "Not provided",
                    pro?.City ?? string.Empty,
                    pro?.State ?? string.Empty,
                    pro?.PostalCode ?? string.Empty,
                    pro?.Units,
                    pro?.Weight,
                    pro?.Cubes,
                    pro?.BillType ?? "Not provided",
                    pro?.AllottedStopSeconds,
                    pro?.Appointment);
            }).ToList();

            notes = parsed.Notes;
            requestFacts = RunSummary.DescribeRequest(requestJson, "PTV");
            stopAudit = RunSummary.ReconcileStops(run.Response.Body, requestJson);
            detailWarning = detail.Unavailable;
        }
        catch (Exception ex)
        {
            // A result PTV returned is still worth showing even if the summary could not be read.
            summaryError = ex.Message;
        }

        return new OptimizationResultDto(
            fileName, run.OptimizationId, optimizedAt, terminal, run.Status ?? "UNKNOWN",
            run.Response.StatusCode, run.Response.StatusLine, run.Elapsed.TotalSeconds, run.TimedOut,
            summary, routeDtos, droppedDtos, notes, requestFacts, stopAudit, log,
            run.Response.Body, summaryError, detailWarning);
    }
}
