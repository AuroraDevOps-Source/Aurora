using System.Globalization;
using Aurora.Contracts;
using System.Text.Json.Nodes;
using static Aurora.Modules.Routing.Optimization.RunSummary;

namespace Aurora.Modules.Routing.Optimization;

public sealed record ManifestProDetail(
    string ProNumber,
    int? StopNumber,
    string ShipToName,
    string Address1,
    string City,
    string State,
    string PostalCode,
    double? Units,
    double? Weight,
    double? Cubes,
    string BillType,
    int? AllottedStopSeconds,
    double? Latitude,
    double? Longitude,
    string? Reason = null,
    AppointmentInfoDto? Appointment = null,
    DateTimeOffset? Arrival = null,
    DateTimeOffset? Departure = null);

public sealed record RouteManifestDetail(
    string Vehicle,
    int? BudgetedSeconds,
    int EstimatedSeconds,
    int Stops,
    double? Units,
    double? Weight,
    double? Cubes,
    IReadOnlyList<ManifestProDetail> Pros,
    DateTimeOffset? Departure,
    DateTimeOffset? Return);

public sealed record ManifestReportDetail(
    DateTimeOffset OptimizedAt,
    string Terminal,
    IReadOnlyDictionary<string, RouteManifestDetail> Routes,
    IReadOnlyDictionary<string, ManifestProDetail> Dropped);

/// <summary>
/// Joins the PTV answer back to the uploaded request so the UI can show a useful quasi-manifest.
/// Standard PTV requests provide the PRO, coordinates, loads, service duration and driver window.
/// Optional FO display fields may be supplied in a root-level "reporting" sidecar; that sidecar is
/// removed before the request is sent to PTV.
/// </summary>
public static class ManifestReportExtractor
{
    public static string RemoveReportingSidecar(string requestJson)
    {
        if (JsonNode.Parse(requestJson) is not JsonObject root || !root.Remove("reporting"))
            return requestJson;
        return root.ToJsonString();
    }

    public static ManifestReportDetail ExtractPtv(string responseJson, string requestJson)
    {
        var response = JsonNode.Parse(responseJson) as JsonObject
            ?? throw new InvalidOperationException("Response is not a JSON object.");
        var request = JsonNode.Parse(requestJson) as JsonObject
            ?? throw new InvalidOperationException("Request is not a JSON object.");
        var reporting = request["reporting"] as JsonObject;

        var locations = Items(request["locations"])
            .OfType<JsonObject>()
            .Where(location => ReadString([location], "id") is not null)
            .ToDictionary(location => ReadString([location], "id")!, StringComparer.Ordinal);
        var orders = Items(request["orders"]?["deliveries"])
            .OfType<JsonObject>()
            .Where(order => ReadString([order], "id") is not null)
            .ToDictionary(order => ReadString([order], "id")!, StringComparer.Ordinal);
        var depotIds = Items(request["depots"])
            .Select(depot => ReadString([depot as JsonObject], "locationId"))
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        var terminal = ReadString([reporting, request], "terminal", "activeTerminal", "originTerminal")
            ?? Items(request["depots"]).Select(depot => ReadString([depot as JsonObject], "locationId", "id")).FirstOrDefault(id => id is not null)
            ?? Items(request["vehicles"]).Select(vehicle => ReadString([vehicle?["start"] as JsonObject], "locationId")).FirstOrDefault(id => id is not null)
            ?? "Not provided";

        var optimizedAt = ParseTime(response["metadata"]?["finished"])
            ?? ParseTime(response["metadata"]?["created"])
            ?? DateTimeOffset.UtcNow;

        var routes = new Dictionary<string, RouteManifestDetail>(StringComparer.Ordinal);
        foreach (var route in Items(response["routes"]).OfType<JsonObject>())
        {
            var vehicleId = ReadString([route], "vehicleId") ?? "?";
            var vehicle = Items(request["vehicles"]).OfType<JsonObject>()
                .FirstOrDefault(candidate => string.Equals(ReadString([candidate], "id"), vehicleId, StringComparison.Ordinal));
            var offset = RoutingSchedule.VehicleOffset(vehicle);
            var pros = new List<ManifestProDetail>();
            var stopNumber = 0;

            foreach (var stop in Items(route["stops"]).OfType<JsonObject>())
            {
                var locationId = ReadString([stop], "locationId");
                if (locationId is null || depotIds.Contains(locationId))
                    continue;

                var orderIds = Items(stop["appointments"])
                    .SelectMany(appointment => Items(appointment["tasks"]))
                    .Select(task => ReadString([task as JsonObject], "orderId"))
                    .Where(id => id is not null)
                    .Select(id => id!)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (orderIds.Count == 0)
                    continue;

                stopNumber++;
                pros.AddRange(orderIds.Select(orderId => BuildPro(orderId, stopNumber, orders, locations, reporting) with
                {
                    Appointment = RoutingInput.Describe(request, orderId),
                    Arrival = RoutingSchedule.InOffset(ParseTime(stop["arrival"]), offset),
                    Departure = RoutingSchedule.InOffset(ParseTime(stop["departure"]), offset)
                }));
            }

            pros = pros.OrderBy(pro => pro.StopNumber).ThenBy(pro => pro.ProNumber, StringComparer.Ordinal).ToList();
            var estimatedSeconds = NullableInt(route["metrics"]?["duration"])
                ?? RouteElapsedSeconds(route)
                ?? 0;
            routes[vehicleId] = new RouteManifestDetail(
                vehicleId,
                VehicleBudgetSeconds(vehicle),
                estimatedSeconds,
                pros.Select(pro => pro.StopNumber).Distinct().Count(),
                Sum(pros, pro => pro.Units),
                Sum(pros, pro => pro.Weight),
                Sum(pros, pro => pro.Cubes),
                pros,
                RoutingSchedule.InOffset(ParseTime(route["start"]?["departure"]), offset),
                RoutingSchedule.InOffset(ParseTime(route["end"]?["arrival"]), offset));
        }

        var dropped = new Dictionary<string, ManifestProDetail>(StringComparer.Ordinal);
        foreach (var unscheduled in Items(response["unscheduledOrders"]).OfType<JsonObject>())
        {
            var orderId = ReadString([unscheduled], "id") ?? "?";
            dropped[orderId] = BuildPro(orderId, null, orders, locations, reporting) with
            {
                Reason = PtvUnscheduledReason(unscheduled),
                Appointment = RoutingInput.Describe(request, orderId)
            };
        }

        return new ManifestReportDetail(optimizedAt, terminal, routes, dropped);
    }

    private static ManifestProDetail BuildPro(
        string orderId,
        int? stopNumber,
        IReadOnlyDictionary<string, JsonObject> orders,
        IReadOnlyDictionary<string, JsonObject> locations,
        JsonObject? reporting)
    {
        orders.TryGetValue(orderId, out var order);
        var delivery = order?["delivery"] as JsonObject;
        var properties = order?["properties"] as JsonObject;
        var locationId = ReadString([delivery], "locationId");
        var location = locationId is not null && locations.TryGetValue(locationId, out var foundLocation)
            ? foundLocation : null;
        var sidecarOrder = FindSidecar(reporting?["orders"], orderId, "proNumber", "id", "orderId");
        var sidecarLocation = locationId is null ? null : FindSidecar(reporting?["locations"], locationId, "id", "locationId");

        var sources = new List<JsonObject?>
        {
            sidecarOrder,
            sidecarOrder?["carrierConsignee"] as JsonObject,
            sidecarOrder?["shipTo"] as JsonObject,
            order?["reporting"] as JsonObject,
            order?["metadata"] as JsonObject,
            order?["carrierConsignee"] as JsonObject,
            order?["shipTo"] as JsonObject,
            order,
            delivery?["reporting"] as JsonObject,
            delivery?["metadata"] as JsonObject,
            delivery,
            properties,
            sidecarLocation,
            sidecarLocation?["address"] as JsonObject,
            location?["reporting"] as JsonObject,
            location?["metadata"] as JsonObject,
            location?["address"] as JsonObject,
            location
        };

        var (latitude, longitude) = Coordinates(location);
        return new ManifestProDetail(
            orderId,
            stopNumber,
            ReadString(sources, "carrierConsigneeName", "shipToName", "consigneeName", "name") ?? "Not provided",
            ReadString(sources, "carrierConsigneeAddress", "carrierConsigneeAddress1", "shipToAddress", "shipToAddress1", "address1", "streetAddress", "street") ?? "Not provided",
            ReadString(sources, "carrierConsigneeCity", "shipToCity", "city") ?? string.Empty,
            ReadString(sources, "carrierConsigneeState", "shipToState", "state", "stateCode") ?? string.Empty,
            ReadString(sources, "carrierConsigneePostalCode", "shipToPostalCode", "postalCode", "zip", "zipCode") ?? string.Empty,
            ReadMetric(sources, order, "units", "unit", "quantity", "pieces"),
            ReadMetric(sources, order, "weight", "weightLb", "totalWeight"),
            ReadMetric(sources, order, "volume", "cubes", "cube", "totalCubes", "cubicFeet"),
            ReadString(sources, "billType", "bill_type") ?? "Not provided",
            NullableInt(delivery?["duration"]),
            latitude,
            longitude);
    }

    private static JsonObject? FindSidecar(JsonNode? node, string id, params string[] idAliases)
    {
        if (node is JsonObject keyed)
        {
            if (keyed[id] is JsonObject exact)
                return exact;
            return keyed.FirstOrDefault(property => string.Equals(property.Key, id, StringComparison.OrdinalIgnoreCase)).Value as JsonObject;
        }

        return Items(node).OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(ReadString([item], idAliases), id, StringComparison.Ordinal));
    }

    private static (double? Latitude, double? Longitude) Coordinates(JsonObject? location) =>
        (ReadDouble([location], "latitude", "lat"), ReadDouble([location], "longitude", "lon", "lng"));

    private static int? VehicleBudgetSeconds(JsonObject? vehicle)
    {
        var start = ParseTime(vehicle?["start"]?["earliestStartTime"]);
        var end = ParseTime(vehicle?["end"]?["latestEndTime"]);
        return start is not null && end is not null && end >= start
            ? (int)Math.Round((end.Value - start.Value).TotalSeconds)
            : null;
    }

    private static int? RouteElapsedSeconds(JsonObject route)
    {
        var start = ParseTime(route["start"]?["departure"] ?? route["start"]?["start"]);
        var end = ParseTime(route["end"]?["arrival"] ?? route["end"]?["end"]);
        return start is not null && end is not null && end >= start
            ? (int)Math.Round((end.Value - start.Value).TotalSeconds)
            : null;
    }

    private static double? Sum(IEnumerable<ManifestProDetail> pros, Func<ManifestProDetail, double?> selector)
    {
        var values = pros.Select(selector).Where(value => value is not null).Select(value => value!.Value).ToList();
        return values.Count == 0 ? null : values.Sum();
    }

    private static double? ReadMetric(IEnumerable<JsonObject?> sources, JsonObject? order, params string[] aliases)
    {
        var direct = ReadDouble(sources, aliases);
        if (direct is not null)
            return direct;

        foreach (var load in Items(order?["properties"]?["loads"]).OfType<JsonObject>())
        {
            var dimension = ReadString([load], "dimension");
            if (dimension is not null && aliases.Any(alias => string.Equals(alias, dimension, StringComparison.OrdinalIgnoreCase)))
                return ReadDouble([load], "value");
        }
        return null;
    }

    private static string? ReadString(IEnumerable<JsonObject?> sources, params string[] aliases)
    {
        foreach (var source in sources)
        {
            if (source is null) continue;
            foreach (var alias in aliases)
            {
                var property = source.FirstOrDefault(item => string.Equals(item.Key, alias, StringComparison.OrdinalIgnoreCase));
                if (property.Value is JsonValue value)
                {
                    if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                    if (value.TryGetValue<double>(out var number))
                        return number.ToString(CultureInfo.InvariantCulture);
                }
            }
        }
        return null;
    }

    private static double? ReadDouble(IEnumerable<JsonObject?> sources, params string[] aliases)
    {
        foreach (var source in sources)
        {
            if (source is null) continue;
            foreach (var alias in aliases)
            {
                var property = source.FirstOrDefault(item => string.Equals(item.Key, alias, StringComparison.OrdinalIgnoreCase));
                if (property.Value is not JsonValue value) continue;
                if (value.TryGetValue<double>(out var number)) return number;
                if (value.TryGetValue<string>(out var text) && double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out number))
                    return number;
            }
        }
        return null;
    }

    private static int? NullableInt(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<int>(out var integer)) return integer;
        if (value.TryGetValue<double>(out var number)) return (int)Math.Round(number);
        if (value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
            return integer;
        return null;
    }

    private static DateTimeOffset? ParseTime(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time : null;
}
