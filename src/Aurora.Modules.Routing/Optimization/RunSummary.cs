using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aurora.Modules.Routing.Optimization;

/// <summary>One finished run, reduced to the numbers that say whether it went well.</summary>
public sealed record RunRecord(
    int Number,
    DateTime When,
    string Source,
    string Status,
    int RoutesUsed,
    int VehiclesOffered,
    int Scheduled,
    int Unscheduled,
    double Km,
    int DriveSeconds,
    double Cost,
    int Violations,
    int Orders,
    int Locations,
    double? CapacityWeight,
    double? CapacityVolume,
    bool FleetMixed)
{
    // Per-order figures are the only ones comparable across runs with different order counts.
    public double CostPerOrder => Scheduled > 0 ? Cost / Scheduled : 0;
    public double KmPerOrder => Scheduled > 0 ? Km / Scheduled : 0;
    public double OrdersPerRoute => RoutesUsed > 0 ? (double)Scheduled / RoutesUsed : 0;

    /// <summary>Compact per-truck capacity for the comparison table; "mixed" when the fleet is not uniform.</summary>
    public string CapacityLabel => FleetMixed ? "mixed"
        : CapacityWeight is null && CapacityVolume is null ? "—"
        : string.Join("/", new[] {
              CapacityWeight is double w ? $"{w:0}w" : null,
              CapacityVolume is double v ? $"{v:0}v" : null
          }.Where(s => s is not null));
}

public sealed record RouteLine(
    string Vehicle, int Orders, int Stops, double Km, int DriveSeconds,
    double Cost, double LoadUtil, double TimeUtil);

public static class RunSummary
{
    /// <summary>
    /// Reads the headline metrics out of a PTV response. <paramref name="requestJson"/> supplies
    /// the fleet size, which lives in the request — "5 of 7 used" is the signal that separates
    /// "it consolidated" from "it ran out of trucks".
    /// </summary>
    /// <summary>
    /// Describes the request that produced a result — the problem PTV was handed, so it can be
    /// read next to the answer. Ends by naming what is deliberately absent, since that is what
    /// makes the result PTV's own work rather than a copy of the input.
    /// </summary>
    public static List<string> DescribeRequest(string requestJson, string provider = "PTV")
    {
        var lines = new List<string>();
        if (JsonNode.Parse(requestJson) is not JsonObject root)
            return ["  (request could not be read)"];

        var locations = (root["locations"] as JsonArray)?.Count ?? 0;
        var depots = (root["depots"] as JsonArray)?.Count ?? 0;
        var deliveries = (root["orders"]?["deliveries"] as JsonArray)?.Where(n => n is not null).Select(n => n!).ToList() ?? [];
        var vehicles = (root["vehicles"] as JsonArray)?.Where(n => n is not null).Select(n => n!).ToList() ?? [];

        lines.Add($"  orders            {deliveries.Count:N0}");
        lines.Add($"  locations         {locations:N0}  (stops plus the depot location)");
        lines.Add($"  depots            {depots:N0}");
        lines.Add($"  trucks offered    {vehicles.Count:N0}");

        // Group identical trucks so a uniform fleet prints as one line and a mixed one is obvious.
        var specs = vehicles.GroupBy(Spec).ToList();
        if (specs.Count == 1 && vehicles.Count > 0)
        {
            // One spec for the whole fleet: spread it over a few readable lines.
            var v = vehicles[0];
            var c = v["costs"];
            lines.Add($"  capacity each     {string.Join(" / ", Items(v["constraints"]?["maximumLoads"])
                .Select(l => $"{Dbl(l["value"]):N0} {Str(l["dimension"])}").DefaultIfEmpty("unlimited"))}");
            lines.Add($"  costs each        {Dbl(c?["perHour"]):C2}/h + {Dbl(c?["perKilometer"]):C2}/km + " +
                      $"{Dbl(c?["perStop"]):C2}/stop + {Dbl(c?["fixed"]):C2} fixed");
            lines.Add($"  routing           {Str(v["routing"]?["profile"])} / {Str(v["routing"]?["trafficMode"])}");
            lines.Add($"  shift             {Str(v["start"]?["earliestStartTime"])} -> {Str(v["end"]?["latestEndTime"])}");
        }
        else if (specs.Count > 1)
        {
            lines.Add($"  fleet             MIXED — {specs.Count} different specs:");
            foreach (var g in specs)
                lines.Add($"                      {g.Count()}x  {g.Key}   [{Trim(string.Join(", ", g.Select(v => Str(v["id"]))), 44)}]");
        }

        var service = deliveries
            .Select(d => (int)Dbl(d["delivery"]?["duration"]) / 60)
            .GroupBy(m => m).OrderByDescending(g => g.Count()).ToList();
        if (service.Count == 1)
            lines.Add($"  service           {service[0].Key} min/order");
        else if (service.Count > 1)
            lines.Add($"  service           MIXED — {string.Join(", ", service.Take(4).Select(g => $"{g.Count()}x {g.Key}min"))}");

        lines.Add($"  calc budget       {(int)Dbl(root["settings"]?["duration"])}s");

        // Demand vs what the fleet can physically hold — this is what sets the truck floor.
        var demand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in deliveries)
            foreach (var l in Items(d["properties"]?["loads"]))
                if (Str(l["dimension"]) is string dim)
                    demand[dim] = demand.GetValueOrDefault(dim) + Dbl(l["value"]);

        var perTruck = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (vehicles.Count > 0)
            foreach (var l in Items(vehicles[0]["constraints"]?["maximumLoads"]))
                if (Str(l["dimension"]) is string dim)
                    perTruck[dim] = Dbl(l["value"]);

        if (demand.Count > 0)
        {
            lines.Add("");
            lines.Add($"  demand            {string.Join(" · ", demand.OrderBy(k => k.Key).Select(k => $"{k.Value:N0} {k.Key}"))}");

            var floor = 1;
            var reasons = new List<string>();
            foreach (var (dim, total) in demand.OrderBy(k => k.Key))
            {
                if (!perTruck.TryGetValue(dim, out var max) || max <= 0) continue;
                var needed = (int)Math.Ceiling(total / max);
                if (needed > floor) { floor = needed; }
                reasons.Add($"{dim} {total:N0}/{max:N0}={needed}");
            }

            if (reasons.Count > 0)
            {
                lines.Add($"  per truck         {string.Join(" · ", perTruck.OrderBy(k => k.Key).Select(k => $"{k.Value:N0} {k.Key}"))}" +
                          (specs.Count > 1 ? "   (first truck — fleet is mixed)" : ""));
                lines.Add($"  capacity floor    {floor} truck(s)   [{string.Join(", ", reasons)}]");
                lines.Add(floor >= vehicles.Count
                    ? $"                    you offered {vehicles.Count}, so capacity alone forces essentially all of them"
                    : $"                    you offered {vehicles.Count}, so {vehicles.Count - floor} are genuinely optional");
            }
        }

        lines.Add("");
        lines.Add($"  NOT in the request: stop sequence and truck assignment. {provider} decides both —");
        lines.Add("  the file states the problem (where, how big, what trucks cost), not the answer.");

        return lines;
    }

    /// <summary>
    /// The problem-side numbers the comparison table needs: how big the ask was, not what PTV did
    /// with it. Fleet capacity is read off the first vehicle and only meaningful when the fleet is
    /// uniform — <see cref="RunRecord.CapacityLabel"/> falls back to "mixed" otherwise.
    /// </summary>
    private static (int Offered, int Orders, int Locations, double? CapWeight, double? CapVolume, bool Mixed)
        ExtractFleetFacts(string requestJson)
    {
        try
        {
            if (JsonNode.Parse(requestJson) is not JsonObject root) return (0, 0, 0, null, null, false);

            var orders = (root["orders"]?["deliveries"] as JsonArray)?.Count(n => n is not null) ?? 0;
            var locations = (root["locations"] as JsonArray)?.Count ?? 0;
            var vehicles = (root["vehicles"] as JsonArray)?.Where(n => n is not null).Select(n => n!).ToList() ?? [];
            var mixed = vehicles.Select(Spec).Distinct().Count() > 1;

            double? w = null, v = null;
            if (vehicles.Count > 0)
                foreach (var l in Items(vehicles[0]["constraints"]?["maximumLoads"]))
                {
                    var dim = Str(l["dimension"]);
                    if (string.Equals(dim, "weight", StringComparison.OrdinalIgnoreCase)) w = Dbl(l["value"]);
                    else if (string.Equals(dim, "volume", StringComparison.OrdinalIgnoreCase)) v = Dbl(l["value"]);
                }

            return (vehicles.Count, orders, locations, w, v, mixed);
        }
        catch (JsonException)
        {
            // Request tab may have been edited to something unparsable since the send; not fatal.
            return (0, 0, 0, null, null, false);
        }
    }

    /// <summary>A one-line signature of a truck, used to tell a uniform fleet from a mixed one.</summary>
    private static string Spec(JsonNode v)
    {
        var loads = Items(v["constraints"]?["maximumLoads"])
            .Select(l => $"{Dbl(l["value"]):N0} {Str(l["dimension"])}")
            .DefaultIfEmpty("no capacity limit");

        var c = v["costs"];
        var shiftStart = Str(v["start"]?["earliestStartTime"]) ?? "?";
        var shiftEnd = Str(v["end"]?["latestEndTime"]) ?? "?";

        return $"{string.Join(" / ", loads)} · " +
               $"{Dbl(c?["perHour"]):N2}/h {Dbl(c?["perKilometer"]):N2}/km {Dbl(c?["perStop"]):N2}/stop {Dbl(c?["fixed"]):N2} fixed · " +
               $"{Str(v["routing"]?["profile"])}/{Str(v["routing"]?["trafficMode"])} · {shiftStart} -> {shiftEnd}";
    }

    internal static string? Str(JsonNode? n)
    {
        try { return n?.GetValue<string>(); }
        catch (Exception) { return n?.ToJsonString(); }
    }

    /// <summary>
    /// Ties the route table's stop counts back to the request's location count. They differ for two
    /// reasons and both are easy to misread: every route counts the depot as an arrival, and a
    /// location served by more than one truck is counted once per truck.
    /// </summary>
    public static List<string> ReconcileStops(string responseJson, string requestJson)
    {
        var lines = new List<string>();
        if (JsonNode.Parse(responseJson) is not JsonObject response) return lines;

        var depotLocations = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (JsonNode.Parse(requestJson) is JsonObject request)
                foreach (var d in Items(request["depots"]))
                    if (Str(d["locationId"]) is string loc) depotLocations.Add(loc);
        }
        catch (JsonException) { /* request unreadable; depot rows just go uncounted */ }

        var visits = new Dictionary<string, int>(StringComparer.Ordinal);
        var arrivals = 0;

        foreach (var route in Items(response["routes"]))
            foreach (var stop in Items(route["stops"]))
            {
                arrivals++;
                if (Str(stop["locationId"]) is string id)
                    visits[id] = visits.GetValueOrDefault(id) + 1;
            }

        if (arrivals == 0) return lines;

        var depotArrivals = visits.Where(kv => depotLocations.Contains(kv.Key)).Sum(kv => kv.Value);
        var shared = visits.Where(kv => !depotLocations.Contains(kv.Key) && kv.Value > 1)
                           .OrderByDescending(kv => kv.Value).ToList();

        lines.Add($"  stops             {arrivals} arrivals over {visits.Count} distinct location(s)");
        if (depotArrivals > 0)
            lines.Add($"                    {depotArrivals} of those are the depot — every route starts there");
        if (shared.Count > 0)
            lines.Add($"                    {shared.Count} location(s) served by more than one truck: " +
                      $"{string.Join(", ", shared.Take(4).Select(kv => $"{kv.Key} x{kv.Value}"))}" +
                      $"{(shared.Count > 4 ? ", …" : "")}");

        return lines;
    }

    public static (RunRecord Record, List<RouteLine> Routes, List<string> Notes) Parse(
        string responseJson, string requestJson, int runNumber, string source)
    {
        var notes = new List<string>();
        var root = JsonNode.Parse(responseJson) as JsonObject
                   ?? throw new InvalidOperationException("Response is not a JSON object.");

        var metrics = root["metrics"] as JsonObject;
        var routeNodes = (root["routes"] as JsonArray)?.Where(n => n is not null).Select(n => n!).ToList() ?? [];

        var routes = routeNodes.Select(r =>
        {
            var m = r["metrics"];
            return new RouteLine(
                Vehicle: r["vehicleId"]?.GetValue<string>() ?? "?",
                Orders: Int(m?["numberOfOrders"]),
                Stops: Int(m?["numberOfStops"]),
                Km: Int(m?["distance"]) / 1000.0,
                DriveSeconds: Int(m?["durations"]?["driving"]),
                Cost: Dbl(m?["costs"]?["total"] ?? m?["cost"]),
                LoadUtil: Dbl(m?["vehicleUtilization"]?["load"]),
                TimeUtil: Dbl(m?["vehicleUtilization"]?["duration"]));
        }).OrderByDescending(r => r.Orders).ToList();

        var (offered, orders, locations, capWeight, capVolume, mixed) = ExtractFleetFacts(requestJson);

        var unscheduled = Int(metrics?["numberOfUnscheduledOrders"]);
        var violations = (root["violations"] as JsonArray)?.Count ?? 0;

        var record = new RunRecord(
            Number: runNumber,
            When: DateTime.Now,
            Source: source,
            Status: root["status"]?.GetValue<string>() ?? "?",
            RoutesUsed: metrics?["numberOfRoutes"] is not null ? Int(metrics["numberOfRoutes"]) : routes.Count,
            VehiclesOffered: offered,
            Scheduled: Int(metrics?["numberOfScheduledOrders"]),
            Unscheduled: unscheduled,
            Km: Int(metrics?["totalDistance"]) / 1000.0,
            DriveSeconds: routes.Sum(r => r.DriveSeconds),
            Cost: Dbl(metrics?["costs"]?["grossTotal"] ?? metrics?["totalCost"]),
            Violations: violations,
            Orders: orders,
            Locations: locations,
            CapacityWeight: capWeight,
            CapacityVolume: capVolume,
            FleetMixed: mixed);

        if (unscheduled > 0)
            notes.Add($"{unscheduled} order(s) UNSCHEDULED — this run did not do the whole job, so its cost is " +
                      "not comparable to a run that placed everything.");

        foreach (var u in (root["unscheduledOrders"] as JsonArray ?? []).Take(8))
            notes.Add($"  unscheduled {Str(u?["id"])}: {PtvUnscheduledReason(u)}");

        if (violations > 0)
            notes.Add($"{violations} constraint violation(s) reported — PTV bent a rule to fit the plan.");

        foreach (var w in (root["warnings"] as JsonArray ?? []).Take(5))
            notes.Add($"  warning: {w?["description"]?.GetValue<string>()}");

        if (root["error"] is JsonObject err)
            notes.Add($"error: {err["errorCode"]?.GetValue<string>()} — {err["description"]?.GetValue<string>()}");

        if (offered > 0 && record.RoutesUsed >= offered)
            notes.Add($"Every one of the {offered} vehicles offered was used — the fleet, not the geography, may be " +
                      "the binding limit. Offer more trucks and re-run to find out.");

        return (record, routes, notes);
    }

    /// <summary>Reads the equivalent headline metrics from a Google OptimizeTours response.</summary>
    public static (RunRecord Record, List<RouteLine> Routes, List<string> Notes) ParseGoogle(
        string responseJson, string requestJson, int runNumber, string source)
    {
        var notes = new List<string>();
        var root = JsonNode.Parse(responseJson) as JsonObject
                   ?? throw new InvalidOperationException("Response is not a JSON object.");
        if (root["error"] is JsonObject error)
            throw new InvalidOperationException(
                $"Google error {Str(error["status"]) ?? Str(error["code"]) ?? "?"}: {Str(error["message"])}");

        var metrics = root["metrics"];
        var aggregate = metrics?["aggregatedRouteMetrics"];
        var routeNodes = Items(root["routes"]).ToList();

        var routes = routeNodes.Select(route =>
        {
            var rm = route["metrics"];
            var visits = Items(route["visits"]).ToList();
            return new RouteLine(
                Vehicle: Str(route["vehicleLabel"]) ?? $"vehicle {Int(route["vehicleIndex"])}",
                Orders: Int(rm?["performedShipmentCount"]) is var performed && performed > 0 ? performed : visits.Count,
                Stops: visits.Count,
                Km: Dbl(rm?["travelDistanceMeters"]) / 1000.0,
                DriveSeconds: DurationSeconds(rm?["travelDuration"]),
                Cost: Dbl(route["routeTotalCost"]),
                LoadUtil: 0,
                TimeUtil: 0);
        }).OrderByDescending(route => route.Orders).ToList();

        var (offered, orders, locations, capWeight, capVolume, mixed) = ExtractFleetFacts(requestJson);
        var skipped = Items(root["skippedShipments"]).ToList();
        var validationErrors = Items(root["validationErrors"]).ToList();
        var scheduled = Int(aggregate?["performedShipmentCount"]);
        if (scheduled == 0 && skipped.Count == 0 && routeNodes.Count > 0)
            scheduled = routes.Sum(route => route.Orders);

        var record = new RunRecord(
            Number: runNumber,
            When: DateTime.Now,
            Source: source,
            Status: validationErrors.Count == 0 ? "SUCCEEDED" : "VALIDATION_ERRORS",
            RoutesUsed: Int(metrics?["usedVehicleCount"]) is var used && used > 0 ? used : routeNodes.Count,
            VehiclesOffered: offered,
            Scheduled: scheduled,
            Unscheduled: skipped.Count,
            Km: Dbl(aggregate?["travelDistanceMeters"]) / 1000.0,
            DriveSeconds: DurationSeconds(aggregate?["travelDuration"]),
            Cost: Dbl(metrics?["totalCost"]),
            Violations: validationErrors.Count,
            Orders: orders,
            Locations: locations,
            CapacityWeight: capWeight,
            CapacityVolume: capVolume,
            FleetMixed: mixed);

        if (skipped.Count > 0)
            notes.Add($"{skipped.Count} order(s) UNSCHEDULED — this run did not do the whole job, so its cost is " +
                      "not comparable to a run that placed everything.");

        foreach (var skippedShipment in skipped.Take(8))
        {
            var label = Str(skippedShipment["label"]);
            if (string.IsNullOrWhiteSpace(label))
            {
                var index = Int(skippedShipment["index"]);
                label = $"shipment {index}";
            }
            notes.Add($"  unscheduled {label}: {GoogleSkippedReason(skippedShipment)}".TrimEnd(':', ' '));
        }

        foreach (var validationError in validationErrors.Take(8))
            notes.Add($"  validation: {Str(validationError["errorMessage"]) ?? validationError.ToJsonString()}");

        var trafficProblems = routeNodes.Count(route =>
            Bool(route["hasTrafficInfeasibilities"]) ||
            Items(route["transitions"]).Any(transition => Bool(transition["trafficInfoUnavailable"])));
        if (trafficProblems > 0)
            notes.Add($"{trafficProblems} route(s) reported unavailable or infeasible traffic timing details.");

        if (offered > 0 && record.RoutesUsed >= offered)
            notes.Add($"Every one of the {offered} vehicles offered was used — the fleet may be the binding limit.");

        return (record, routes, notes);
    }

    public static string Format(RunRecord r, List<RouteLine> routes, List<string> notes,
                               IReadOnlyList<RunRecord> history, List<string>? requestFacts = null,
                               List<string>? stopAudit = null, string provider = "PTV")
    {
        var sb = new StringBuilder();

        sb.AppendLine($"=== Run {r.Number} · {r.When:yyyy-MM-dd HH:mm:ss} · {r.Source} ===");

        if (requestFacts is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("--- What was sent (the problem) ---");
            sb.AppendLine();
            foreach (var line in requestFacts) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine($"--- What came back ({provider}'s answer) ---");
        }

        sb.AppendLine();
        sb.AppendLine($"  status          {r.Status}");
        sb.AppendLine($"  routes used     {r.RoutesUsed}" + (r.VehiclesOffered > 0 ? $" of {r.VehiclesOffered} offered" : ""));
        sb.AppendLine($"  orders          {r.Scheduled:N0} scheduled, {r.Unscheduled:N0} unscheduled");
        if (stopAudit is { Count: > 0 })
            foreach (var line in stopAudit) sb.AppendLine(line);

        sb.AppendLine($"  distance        {r.Km:N0} km  ({r.Km * 0.621371:N0} mi)");
        sb.AppendLine($"  drive time      {Hm(r.DriveSeconds)}");
        sb.AppendLine($"  total cost      {r.Cost:C2}");
        sb.AppendLine();
        sb.AppendLine($"  per order       {r.CostPerOrder:C2} · {r.KmPerOrder:N1} km");
        sb.AppendLine($"  per route       {r.OrdersPerRoute:N1} orders");

        if (routes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  route         orders  stops      km    drive        cost   load%   time%");
            sb.AppendLine("  " + new string('-', 74));
            foreach (var x in routes)
                sb.AppendLine($"  {Trim(x.Vehicle, 12),-12}  {x.Orders,6}  {x.Stops,5}  {x.Km,6:N0}  {Hm(x.DriveSeconds),7}  " +
                              $"{x.Cost,10:C2}  {x.LoadUtil,5:P0}  {x.TimeUtil,5:P0}");
        }

        if (notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  Worth reading:");
            foreach (var n in notes) sb.AppendLine($"  - {n}");
        }

        if (history.Count > 1)
        {
            sb.AppendLine();
            sb.AppendLine($"=== Run history (this session) ===");
            sb.AppendLine();
            // Built from the same field widths as the row below, not hand-typed, so the two
            // cannot drift out of alignment when a column's width changes.
            var header = $"  {"#",2}  {"time",-8}  {"source",-20}  " +
                         $"{"ordrs",5}  {"locs",4}  {"trks",4}  {"cap w/v",-13}  " +
                         $"{"used",4}  {"sched",5}  {"unsch",5}  {"km",6}  {"cost",10}  " +
                         $"{"$/order",8}  {"ord/route",9}";
            sb.AppendLine(header);
            sb.AppendLine("  " + new string('-', header.Length - 2));
            foreach (var h in history)
            {
                var mark = h.Number == r.Number ? ">" : " ";
                sb.AppendLine($" {mark}{h.Number,2}  {h.When:HH:mm:ss}  {Trim(h.Source, 20),-20}  " +
                              $"{h.Orders,5}  {h.Locations,4}  {h.VehiclesOffered,4}  {h.CapacityLabel,-13}  " +
                              $"{h.RoutesUsed,4}  {h.Scheduled,5}  {h.Unscheduled,5}  {h.Km,6:N0}  {h.Cost,10:C2}  " +
                              $"{h.CostPerOrder,8:C2}  {h.OrdersPerRoute,9:N1}");
            }

            sb.AppendLine();
            sb.AppendLine("  ordrs / locs / trks / cap describe the PROBLEM each run solved. If those differ between");
            sb.AppendLine("  two rows, the runs are not the same problem — cost and km are only comparable when they");
            sb.AppendLine($"  match. used / sched / unsch / km / cost is what {provider} did with it. A run with unscheduled");
            sb.AppendLine("  orders is never 'cheaper' — it just did less work.");
        }

        sb.AppendLine();
        sb.AppendLine("  Distance and cost are only as good as the coordinates in the request. Comparisons");
        sb.AppendLine("  between runs over the same stops hold regardless of how those were derived.");

        return sb.ToString();
    }

    /// <summary>The reason text for one PTV unscheduled order — shared by the plain-text notes and the HTML dropped-orders section.</summary>
    internal static string PtvUnscheduledReason(JsonNode? order)
    {
        var reasons = (order?["details"] as JsonArray ?? [])
            .Select(d => d?["reason"]?.GetValue<string>()).Where(r => r is not null);
        return $"{Str(order?["schedulability"])} — {string.Join("; ", reasons)}";
    }

    /// <summary>The reason text for one Google skipped shipment — shared by the plain-text notes and the HTML dropped-orders section.</summary>
    internal static string GoogleSkippedReason(JsonNode? skipped) =>
        string.Join("; ", Items(skipped?["reasons"]).Select(Str).Where(reason => reason is not null));

    internal static IEnumerable<JsonNode> Items(JsonNode? node) =>
        node is JsonArray array ? array.Where(n => n is not null).Select(n => n!) : [];

    internal static string Hm(int seconds) =>
        seconds <= 0 ? "—" : $"{seconds / 3600}h {seconds % 3600 / 60:D2}m";

    internal static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    internal static int Int(JsonNode? n)
    {
        try { return n is null ? 0 : (int)Math.Round(n.GetValue<double>()); }
        catch (Exception) { return 0; }
    }

    internal static double Dbl(JsonNode? n)
    {
        try { return n?.GetValue<double>() ?? 0; }
        catch (Exception) { return 0; }
    }

    internal static bool Bool(JsonNode? n)
    {
        try { return n?.GetValue<bool>() ?? false; }
        catch (Exception) { return false; }
    }

    private static int DurationSeconds(JsonNode? n)
    {
        var text = Str(n);
        if (string.IsNullOrWhiteSpace(text) || !text.EndsWith('s')) return 0;
        return double.TryParse(text[..^1], System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? (int)Math.Round(seconds)
            : 0;
    }
}
