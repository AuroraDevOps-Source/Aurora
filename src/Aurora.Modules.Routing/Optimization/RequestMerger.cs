using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aurora.Modules.Routing.Optimization;

/// <summary>
/// Combines several OptiFlow requests into one routing problem.
///
/// Locations and depots are unioned by id — two manifests delivering into the same ZIP
/// collapse to a single stop, which is where PTV gets the room to consolidate. Delivery
/// orders are concatenated.
///
/// The fleet is NOT concatenated. Per-file fleets share ids, and adding them together would
/// presuppose the answer (offering exactly as many trucks as the separate runs used). Instead
/// every distinct vehicle is kept and the fleet is topped up to the combined demand's capacity
/// floor plus a few spares, so "could this be done with fewer trucks?" stays an open question.
/// </summary>
public static class RequestMerger
{
    public sealed record Result(string Json, List<string> Log, List<string> Warnings);

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Result Merge(IReadOnlyList<(string Name, string Json)> files, int spareVehicles = 2)
    {
        var log = new List<string>();
        var warnings = new List<string>();

        var locations = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var depots = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var vehicles = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var deliveries = new List<JsonNode>();
        var orderIds = new HashSet<string>(StringComparer.Ordinal);
        var calcSeconds = 0;

        foreach (var (name, json) in files)
        {
            JsonObject? obj;
            try
            {
                obj = JsonNode.Parse(json) as JsonObject;
            }
            catch (JsonException ex)
            {
                warnings.Add($"{name}: not valid JSON, skipped — {ex.Message}");
                continue;
            }

            if (obj is null)
            {
                warnings.Add($"{name}: root is not a JSON object, skipped.");
                continue;
            }

            var newLocations = 0;
            foreach (var location in Items(obj["locations"]))
            {
                var id = Id(location);
                if (id is null) continue;

                if (locations.TryGetValue(id, out var seen))
                {
                    if (!SameCoordinates(seen, location))
                        warnings.Add($"{name}: location '{id}' has different coordinates than an earlier file — kept the first.");
                    continue;
                }

                locations[id] = location.DeepClone();
                newLocations++;
            }

            var newOrders = 0;
            foreach (var order in Items(obj["orders"]?["deliveries"]))
            {
                var id = Id(order);
                if (id is null) continue;

                if (!orderIds.Add(id))
                {
                    warnings.Add($"{name}: order '{id}' already came from an earlier file — skipped the duplicate.");
                    continue;
                }

                deliveries.Add(order.DeepClone());
                newOrders++;
            }

            foreach (var depot in Items(obj["depots"]))
            {
                var id = Id(depot);
                if (id is null) continue;

                if (depots.TryGetValue(id, out var seenDepot))
                {
                    var a = seenDepot["locationId"]?.GetValue<string>();
                    var b = depot["locationId"]?.GetValue<string>();
                    if (a != b)
                        warnings.Add($"{name}: depot '{id}' points at location '{b}' but an earlier file " +
                                     $"pointed it at '{a}' — kept '{a}'. The trucks from this file will start " +
                                     "from the wrong place.");
                    continue;
                }

                depots[id] = depot.DeepClone();
            }

            foreach (var vehicle in Items(obj["vehicles"]))
            {
                var id = Id(vehicle);
                if (id is null) continue;

                if (vehicles.TryGetValue(id, out var seenVehicle))
                {
                    if (Spec(seenVehicle) != Spec(vehicle))
                        warnings.Add($"{name}: vehicle '{id}' is specified differently here than in an earlier " +
                                     "file — kept the first. Give the trucks distinct ids if they are meant to " +
                                     "be different trucks.");
                    continue;
                }

                vehicles[id] = vehicle.DeepClone();
            }

            if (obj["settings"]?["duration"]?.GetValue<int>() is int d)
                calcSeconds = Math.Max(calcSeconds, d);

            log.Add($"  {name}: +{newOrders} orders, +{newLocations} new locations");
        }

        if (deliveries.Count == 0)
            throw new InvalidOperationException("None of the selected files contained any delivery orders.");
        if (vehicles.Count == 0)
            throw new InvalidOperationException("None of the selected files contained any vehicles to use as a fleet template.");

        // Trucks with different ids are legitimately allowed to differ — PTV supports a mixed
        // fleet — but it changes how the result reads, and the spares cloned below all inherit
        // the FIRST truck's spec. Silent is the wrong behaviour here.
        var distinctSpecs = vehicles.Values.GroupBy(Spec).ToList();
        if (distinctSpecs.Count > 1)
        {
            warnings.Add($"The merged fleet is MIXED — {distinctSpecs.Count} different truck specs across " +
                         $"{vehicles.Count} trucks (capacity, costs, routing profile or shift differ). PTV allows " +
                         "this, but any spare trucks added below copy the first truck's spec, and cost comparisons " +
                         "get muddy when trucks are not priced alike.");
            foreach (var g in distinctSpecs)
                log.Add($"  spec x{g.Count()}: {g.Key}");
        }

        var fleet = BuildFleet(vehicles, deliveries, spareVehicles, log, warnings);

        var merged = new JsonObject
        {
            ["settings"] = new JsonObject { ["duration"] = calcSeconds > 0 ? calcSeconds : 30 },
            ["locations"] = new JsonArray(locations.Values.Select(v => v.DeepClone()).ToArray()),
            ["orders"] = new JsonObject
            {
                ["deliveries"] = new JsonArray(deliveries.Select(d => d.DeepClone()).ToArray())
            },
            ["vehicles"] = new JsonArray(fleet.Select(v => v.DeepClone()).ToArray())
        };

        if (depots.Count > 0)
            merged["depots"] = new JsonArray(depots.Values.Select(v => v.DeepClone()).ToArray());

        log.Insert(0, $"Merged {files.Count} files -> {deliveries.Count} orders, " +
                     $"{locations.Count} locations, {fleet.Count} vehicles, calc budget {calcSeconds}s");

        if (calcSeconds > 0 && calcSeconds < 60 && deliveries.Count > 100)
            log.Add($"  note: calc budget is only {calcSeconds}s for {deliveries.Count} orders — " +
                    "raise settings.duration in the Request tab for a better answer.");

        // One depot means one terminal. Several means multi-depot routing, which behaves
        // differently enough that it should not pass silently.
        if (depots.Count > 1)
            warnings.Add($"{depots.Count} depots merged — vehicles start/end at whichever location their own " +
                         "definition names, so this is a multi-depot problem. Check that is what you want.");

        return new Result(merged.ToJsonString(Indented), log, warnings);
    }

    /// <summary>
    /// Keeps every distinct vehicle from the inputs, then tops the fleet up to the combined
    /// demand's capacity floor plus <paramref name="spares"/>.
    /// </summary>
    private static List<JsonNode> BuildFleet(
        Dictionary<string, JsonNode> unique,
        List<JsonNode> deliveries,
        int spares,
        List<string> log,
        List<string> warnings)
    {
        var fleet = unique.Values.ToList();
        var template = fleet[0];

        var demand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var order in deliveries)
            foreach (var load in Items(order["properties"]?["loads"]))
                if (load["dimension"]?.GetValue<string>() is string dim)
                    demand[dim] = demand.GetValueOrDefault(dim) + (load["value"]?.GetValue<double>() ?? 0);

        var capacity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var load in Items(template["constraints"]?["maximumLoads"]))
            if (load["dimension"]?.GetValue<string>() is string dim)
                capacity[dim] = load["value"]?.GetValue<double>() ?? 0;

        var floor = 1;
        var reasons = new List<string>();
        foreach (var (dim, total) in demand.OrderBy(kv => kv.Key))
        {
            if (!capacity.TryGetValue(dim, out var max) || max <= 0)
            {
                warnings.Add($"orders demand '{dim}' but the vehicle template sets no maximum for it — " +
                             "that dimension cannot constrain the fleet size.");
                continue;
            }

            var needed = (int)Math.Ceiling(total / max);
            floor = Math.Max(floor, needed);
            reasons.Add($"{dim} {total:N0}/{max:N0} => {needed}");
        }

        var target = floor + Math.Max(0, spares);
        log.Add($"  fleet: {unique.Count} distinct from the files, capacity floor {floor} " +
                $"({string.Join(", ", reasons)}), offering {Math.Max(target, fleet.Count)}");

        var n = 0;
        while (fleet.Count < target)
        {
            string id;
            do { id = $"TRUCK_{++n}"; } while (unique.ContainsKey(id));

            var clone = template.DeepClone();
            clone["id"] = id;
            fleet.Add(clone);
            unique[id] = clone;
        }

        return fleet;
    }

    /// <summary>Signature of a truck's routing-relevant settings, for spotting disagreement.</summary>
    private static string Spec(JsonNode v)
    {
        var loads = string.Join("/", Items(v["constraints"]?["maximumLoads"])
            .Select(l => $"{l["dimension"]}:{l["value"]}"));
        var c = v["costs"];
        return $"[{loads}] {c?["perHour"]}/{c?["perKilometer"]}/{c?["perStop"]}/{c?["fixed"]} " +
               $"{v["routing"]?["profile"]}/{v["routing"]?["trafficMode"]} " +
               $"{v["start"]?["earliestStartTime"]}->{v["end"]?["latestEndTime"]}";
    }

    private static IEnumerable<JsonNode> Items(JsonNode? node) =>
        node is JsonArray array
            ? array.Where(n => n is not null).Select(n => n!)
            : [];

    private static string? Id(JsonNode node) =>
        node["id"]?.GetValue<string>() is string id && !string.IsNullOrWhiteSpace(id) ? id : null;

    private static bool SameCoordinates(JsonNode a, JsonNode b) =>
        Coord(a, "latitude") == Coord(b, "latitude") && Coord(a, "longitude") == Coord(b, "longitude");

    private static double? Coord(JsonNode node, string name) =>
        node[name]?.GetValue<double>();
}
