using System.Text.Json.Nodes;
using static Aurora.Modules.Routing.Optimization.RunSummary;

namespace Aurora.Modules.Routing.Optimization;

/// <summary>One stop in visit order, with the coordinates the map and manifest need.</summary>
public sealed record RouteStopDetail(
    string LocationId, double Latitude, double Longitude,
    DateTimeOffset? Arrival, DateTimeOffset? Departure,
    bool IsDepot, IReadOnlyList<string> OrderIds);

public sealed record RouteDetail(string Vehicle, IReadOnlyList<RouteStopDetail> Stops);

public sealed record DroppedOrder(string OrderId, double? Latitude, double? Longitude, string Reason);

/// <summary>
/// Per-stop detail for the Summary webpage's manifest and map. <see cref="Unavailable"/> is set
/// instead of throwing when a response's stop-level shape can't be read — the stat cards, aggregate
/// route table and notes come from <see cref="RunSummary"/> independently, so a map failure here
/// should never take those down with it.
/// </summary>
public sealed record RouteDetailResult(
    IReadOnlyList<RouteDetail> Routes, IReadOnlyList<DroppedOrder> Dropped, string? Unavailable);

/// <summary>
/// Builds the per-stop detail that <see cref="RunSummary"/>'s aggregate parsing does not carry.
/// Coordinates and order metadata always come from the canonical PTV-shaped request — the same
/// one <see cref="RunSummary.DescribeRequest"/> reads — never from a provider-translated request,
/// so this works the same way regardless of which engine produced the response.
/// </summary>
public static class RouteDetailExtractor
{
    public static RouteDetailResult ExtractPtv(string responseJson, string requestJson)
    {
        try
        {
            var response = JsonNode.Parse(responseJson) as JsonObject
                ?? throw new InvalidOperationException("Response is not a JSON object.");
            var request = JsonNode.Parse(requestJson) as JsonObject ?? new JsonObject();
            var (coords, depotIds, orderLocation) = CanonicalLookups(request);

            var routes = new List<RouteDetail>();
            foreach (var route in Items(response["routes"]))
            {
                var vehicle = Str(route["vehicleId"]) ?? "?";
                var sourceVehicle = Items(request["vehicles"]).OfType<JsonObject>().FirstOrDefault(v => Str(v["id"]) == vehicle);
                var offset = RoutingSchedule.VehicleOffset(sourceVehicle);
                var stops = new List<RouteStopDetail>();
                var stopNodes = Items(route["stops"]).ToList();

                if (stopNodes.Count > 0)
                {
                    foreach (var stop in stopNodes)
                    {
                        var orderIds = Items(stop["appointments"])
                            .SelectMany(a => Items(a["tasks"]))
                            .Select(t => Str(t["orderId"]))
                            .Where(id => id is not null).Select(id => id!).Distinct().ToList();
                        if (BuildStop(stop["locationId"], stop["arrival"], stop["departure"], orderIds, coords, depotIds) is { } detail)
                            stops.Add(detail);
                    }

                    // route.end is the return-to-depot leg; PTV never repeats it in stops[], so
                    // a map built from stops[] alone would silently omit the last leg.
                    if (route["end"] is JsonObject end &&
                        BuildStop(end["locationId"], end["arrival"], null, [], coords, depotIds) is { } endDetail)
                        stops.Add(endDetail);
                }
                else
                {
                    // A vehicle with nothing dispatched still has start/end — show it rather than
                    // dropping it from the map entirely.
                    if (route["start"] is JsonObject start &&
                        BuildStop(start["locationId"], null, start["departure"], [], coords, depotIds) is { } startDetail)
                        stops.Add(startDetail);
                    if (route["end"] is JsonObject end &&
                        BuildStop(end["locationId"], end["arrival"], null, [], coords, depotIds) is { } endDetail)
                        stops.Add(endDetail);
                }

                if (stops.Count > 0)
                    routes.Add(new RouteDetail(vehicle, stops.Select(stop => stop with
                    {
                        Arrival = RoutingSchedule.InOffset(stop.Arrival, offset),
                        Departure = RoutingSchedule.InOffset(stop.Departure, offset)
                    }).ToList()));
            }

            var dropped = new List<DroppedOrder>();
            foreach (var order in Items(response["unscheduledOrders"]))
            {
                var id = Str(order["id"]) ?? "?";
                var (lat, lon) = LookUp(id, orderLocation, coords);
                dropped.Add(new DroppedOrder(id, lat, lon, PtvUnscheduledReason(order)));
            }

            return new RouteDetailResult(routes, dropped, null);
        }
        catch (Exception ex)
        {
            return new RouteDetailResult([], [], $"Route detail unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Unverified against a real Google response in this environment (Google isn't credentialed
    /// here, and no sample response exists in the repo) — degrades to <see cref="RouteDetailResult.Unavailable"/>
    /// rather than throwing when the expected visit shape isn't found.
    /// </summary>
    public static RouteDetailResult ExtractGoogle(string responseJson, string requestJson)
    {
        try
        {
            var response = JsonNode.Parse(responseJson) as JsonObject
                ?? throw new InvalidOperationException("Response is not a JSON object.");
            var request = JsonNode.Parse(requestJson) as JsonObject ?? new JsonObject();
            var (coords, depotIds, orderLocation) = CanonicalLookups(request);

            // GoogleLvrTranslator builds shipments/vehicles by iterating these same two arrays in
            // file order and copying each id into "label" 1:1, so a response's shipmentIndex/
            // vehicleIndex is a safe positional fallback into them when shipmentLabel is missing.
            var sourceDeliveryIds = Items(request["orders"]?["deliveries"]).Select(o => Str(o["id"])).ToList();
            var sourceVehicles = Items(request["vehicles"]).ToList();

            var routes = new List<RouteDetail>();
            var totalVisits = 0;
            var resolvedVisits = 0;

            foreach (var route in Items(response["routes"]))
            {
                var vehicleId = Str(route["vehicleLabel"]);
                var vehicleNode = vehicleId is not null
                    ? sourceVehicles.FirstOrDefault(v => Str(v["id"]) == vehicleId)
                    : null;
                if (vehicleNode is null && TryIndex(route["vehicleIndex"], out var vIndex) &&
                    vIndex < sourceVehicles.Count)
                {
                    vehicleNode = sourceVehicles[vIndex];
                    vehicleId ??= Str(vehicleNode["id"]);
                }

                var stops = new List<RouteStopDetail>();
                if (vehicleNode is not null &&
                    BuildStop(vehicleNode["start"]?["locationId"], null, null, [], coords, depotIds) is { } startDetail)
                    stops.Add(startDetail);

                foreach (var visit in Items(route["visits"]))
                {
                    totalVisits++;
                    var orderId = Str(visit["shipmentLabel"]);
                    if (string.IsNullOrWhiteSpace(orderId))
                    {
                        orderId = TryIndex(visit["shipmentIndex"], out var sIndex) && sIndex < sourceDeliveryIds.Count
                            ? sourceDeliveryIds[sIndex] : null;
                    }
                    if (orderId is null || !orderLocation.TryGetValue(orderId, out var locationId) || locationId is null)
                        continue;

                    if (BuildStop(locationId, visit["startTime"], null, [orderId], coords, depotIds) is { } stopDetail)
                    {
                        stops.Add(stopDetail);
                        resolvedVisits++;
                    }
                }

                if (vehicleNode is not null &&
                    BuildStop(vehicleNode["end"]?["locationId"], null, null, [], coords, depotIds) is { } endDetail)
                    stops.Add(endDetail);

                if (stops.Count > 0)
                    routes.Add(new RouteDetail(vehicleId ?? "?", stops));
            }

            if (totalVisits > 0 && resolvedVisits == 0)
                return new RouteDetailResult([], [],
                    "Could not resolve per-stop detail for this Google response — showing totals only.");

            var dropped = new List<DroppedOrder>();
            foreach (var skipped in Items(response["skippedShipments"]))
            {
                var orderId = Str(skipped["label"]);
                var hasIndex = TryIndex(skipped["index"], out var index);
                if (string.IsNullOrWhiteSpace(orderId))
                    orderId = hasIndex && index < sourceDeliveryIds.Count ? sourceDeliveryIds[index]
                        : hasIndex ? $"shipment {index}" : "shipment ?";
                var (lat, lon) = LookUp(orderId!, orderLocation, coords);
                dropped.Add(new DroppedOrder(orderId!, lat, lon, GoogleSkippedReason(skipped)));
            }

            return new RouteDetailResult(routes, dropped, null);
        }
        catch (Exception ex)
        {
            return new RouteDetailResult([], [], $"Route detail unavailable: {ex.Message}");
        }
    }

    private static (Dictionary<string, (double Lat, double Lon)> Coords, HashSet<string> DepotIds,
        Dictionary<string, string?> OrderLocation) CanonicalLookups(JsonObject request)
    {
        var coords = new Dictionary<string, (double Lat, double Lon)>(StringComparer.Ordinal);
        foreach (var loc in Items(request["locations"]))
            if (Str(loc["id"]) is string id)
                coords[id] = (Dbl(loc["latitude"]), Dbl(loc["longitude"]));

        var depotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var depot in Items(request["depots"]))
            if (Str(depot["locationId"]) is string locId)
                depotIds.Add(locId);

        var orderLocation = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var order in Items(request["orders"]?["deliveries"]))
            if (Str(order["id"]) is string id)
                orderLocation[id] = Str(order["delivery"]?["locationId"]);

        return (coords, depotIds, orderLocation);
    }

    /// <summary>
    /// True only when <paramref name="n"/> is actually present — unlike <see cref="RunSummary.Int"/>,
    /// a missing field must not silently resolve as index 0 (the first item is almost always a valid
    /// index into a non-empty array, which would mask a response shape this extractor can't read).
    /// </summary>
    private static bool TryIndex(JsonNode? n, out int index)
    {
        index = n is null ? -1 : Int(n);
        return n is not null && index >= 0;
    }

    private static (double? Lat, double? Lon) LookUp(string orderId,
        Dictionary<string, string?> orderLocation, Dictionary<string, (double Lat, double Lon)> coords) =>
        orderLocation.TryGetValue(orderId, out var locationId) && locationId is not null &&
        coords.TryGetValue(locationId, out var c) ? (c.Lat, c.Lon) : (null, null);

    private static RouteStopDetail? BuildStop(JsonNode? locationIdNode, JsonNode? arrival, JsonNode? departure,
        IReadOnlyList<string> orderIds, Dictionary<string, (double Lat, double Lon)> coords, HashSet<string> depotIds)
    {
        if (Str(locationIdNode) is not string locationId || !coords.TryGetValue(locationId, out var c))
            return null;
        return new RouteStopDetail(locationId, c.Lat, c.Lon, ParseTime(arrival), ParseTime(departure),
            depotIds.Contains(locationId), orderIds);
    }

    private static DateTimeOffset? ParseTime(JsonNode? n) =>
        Str(n) is string s && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt) ? dt : null;
}
