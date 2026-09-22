using System.Text.Json.Nodes;
using Aurora.Contracts;
using static Aurora.Contracts.RoutingInput;

namespace Aurora.Modules.Routing.Optimization;

/// <summary>Build input routes from a completed result after applying the current appointments.
/// See PTV OptiFlow's RouteStructure and TaskStructure schemas. Result stops are not input routes.
/// </summary>
public static class QuickUpdate
{
    public const int CalculationSeconds = 5;

    public static string Prepare(string preparedJson, string previousResult)
    {
        var request = Parse(preparedJson);
        if (Items(request["routes"]).Any())
            throw new FormatException("Quick update requires plan inputs without preassigned routes. Use Full optimization for this file.");
        var result = Parse(previousResult);
        if (Text(result["status"]) != "SUCCEEDED")
            throw new FormatException("Quick update needs a successfully completed previous plan.");

        var vehicles = Items(request["vehicles"]).ToDictionary(x => Text(x["id"])!);
        var orders = Items(request["orders"]?["deliveries"]).ToDictionary(x => Text(x["id"])!);
        var locations = Items(request["locations"]).ToDictionary(x => Text(x["id"])!);
        var depots = Items(request["depots"]).ToDictionary(x => Text(x["id"])!);
        var routes = new JsonArray();
        var seenVehicles = new HashSet<string>();
        var seenTasks = new HashSet<(string, string)>();
        foreach (var previous in Items(result["routes"]))
        {
            var vehicleId = Text(previous["vehicleId"]);
            if (vehicleId is null || !vehicles.TryGetValue(vehicleId, out var vehicle)) continue;
            if (!seenVehicles.Add(vehicleId)) throw new FormatException("Quick update cannot reuse multiple routes for the same vehicle. Use Full optimization.");
            var tasks = new JsonArray();
            var omittedOrders = new HashSet<string>();
            foreach (var stop in Items(previous["stops"]))
            foreach (var appointment in Items(stop["appointments"]))
            foreach (var task in Items(appointment["tasks"]))
            {
                var orderId = Text(task["orderId"]);
                var type = Text(task["type"]);
                if (orderId is null || !orders.TryGetValue(orderId, out var order)) continue;
                if (type is not ("PICKUP" or "DELIVERY"))
                    throw new FormatException("The previous plan contains an unsupported task. Use Full optimization.");
                if (!seenTasks.Add((orderId, type))) throw new FormatException("The previous plan contains duplicate tasks.");
                var input = new JsonObject { ["orderId"] = orderId, ["type"] = type };
                JsonObject? location = null;
                JsonObject? currentTask = null;
                if (type == "PICKUP")
                {
                    var depotId = Text(task["depotId"]);
                    if (depotId is null || !depots.TryGetValue(depotId, out var depot))
                    { omittedOrders.Add(orderId); continue; }
                    input["depotId"] = depotId;
                    currentTask = depot;
                    locations.TryGetValue(Text(depot["locationId"]) ?? "", out location);
                }
                else
                {
                    currentTask = order["delivery"] as JsonObject;
                    locations.TryGetValue(Text(currentTask?["locationId"]) ?? "", out location);
                }
                var slots = Items(location?["stopProperties"]?["timeSlots"]).ToList();
                var allowed = (currentTask?["timeSlotIds"] as JsonArray)?.Select(Text).ToHashSet();
                if (slots.Count > 0)
                {
                    var eligible = slots.Where(x => allowed is not { Count: > 0 } || allowed.Contains(Text(x["id"]))).ToList();
                    var slot = eligible.FirstOrDefault(x => Text(x["id"]) == Text(appointment["timeSlotId"])) ?? eligible.FirstOrDefault();
                    if (slot is null) { omittedOrders.Add(orderId); continue; }
                    input["timeSlotId"] = Text(slot["id"]);
                }
                // Let PTV rebuild timing, compartments, breaks and charging from the NEW constraints.
                tasks.Add(input);
            }
            foreach (var task in tasks.OfType<JsonObject>().Where(x => omittedOrders.Contains(Text(x["orderId"])!)).ToList())
                tasks.Remove(task);
            if (tasks.Count == 0) continue;
            var start = Text(vehicle["start"]?["earliestStartTime"]) ?? Text(previous["start"]?["start"]) ?? Text(previous["start"]?["departure"]);
            if (start is null) throw new FormatException("The previous route has no start time. Use Full optimization.");
            routes.Add(new JsonObject
            {
                ["vehicleId"] = vehicleId, ["start"] = start, ["tasks"] = tasks,
                ["reconstructionPolicy"] = new JsonObject { ["violations"] = "CLEANUP" }
            });
        }
        // New orders and orders on removed trucks remain in the request for the solver to assign.
        request["routes"] = routes;
        request["settings"] ??= new JsonObject();
        request["settings"]!["duration"] = CalculationSeconds;
        return request.ToJsonString();
    }
}
