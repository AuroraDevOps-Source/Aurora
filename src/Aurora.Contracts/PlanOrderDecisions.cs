using System.Text.Json.Nodes;

namespace Aurora.Contracts;

/// <summary>Planner intent stays in reporting; only prepared copies filter orders and add solver constraints.</summary>
public static class PlanOrderDecisions
{
    public const string Excluded = "exclude";
    public const string LeftOff = "left-off";

    // A manual addition must not reopen every other unscheduled order when its truck gets overtime.
    // Keep this intent in the original request so it survives repeated runs and session recovery.
    public static string PreserveLeftOff(string json, OptimizationResultDto result)
    {
        var root = RoutingInput.Parse(json);
        if (root["reporting"]?["orderDecisions"] is not JsonObject decisions
            || !decisions.Any(d => RoutingInput.Text(d.Value)?.StartsWith("truck:", StringComparison.Ordinal) == true)) return json;
        var routed = result.Routes.SelectMany(r => r.Pros).Select(p => p.ProNumber).ToHashSet(StringComparer.Ordinal);
        foreach (var order in RoutingInput.Items(root["orders"]?["deliveries"]))
        {
            var id = RoutingInput.Text(order["id"])!;
            if (!routed.Contains(id) && Get(root, id) == "") decisions[id] = LeftOff;
        }
        return root.ToJsonString();
    }
    public static string Get(JsonObject root, string orderId) => RoutingInput.Text(root["reporting"]?["orderDecisions"]?[orderId]) ?? "";

    public static IReadOnlyList<string> UnscheduledPins(string json, OptimizationResultDto result)
    {
        var root = RoutingInput.Parse(json);
        if (root["reporting"]?["orderDecisions"] is not JsonObject decisions) return [];
        return decisions.Where(d => RoutingInput.Text(d.Value) is { } value && value.StartsWith("truck:", StringComparison.Ordinal)
            && !result.Routes.Any(r => "truck:" + r.Vehicle == value && r.Pros.Any(p => p.ProNumber == d.Key)))
            .Select(d => d.Key).ToList();
    }

    public static string Set(string json, string orderId, string decision)
    {
        var root = RoutingInput.Parse(json);
        if (!RoutingInput.Items(root["orders"]?["deliveries"]).Any(o => RoutingInput.Text(o["id"]) == orderId))
            throw new FormatException("Choose an order from this plan.");
        if (decision != "" && decision != Excluded && !RoutingInput.Items(root["vehicles"]).Any(v => "truck:" + RoutingInput.Text(v["id"]) == decision))
            throw new FormatException("Choose an available truck.");
        root["reporting"] ??= new JsonObject();
        root["reporting"]!["orderDecisions"] ??= new JsonObject();
        var decisions = root["reporting"]!["orderDecisions"]!.AsObject();
        if (decision == "") decisions.Remove(orderId); else decisions[orderId] = decision;
        return root.ToJsonString();
    }

    public static string Prepare(string json)
    {
        var root = RoutingInput.Parse(json);
        if (root["reporting"]?["orderDecisions"] is not JsonObject decisions || decisions.Count == 0) return json;
        if (RoutingInput.Items(root["routes"]).Any()) throw new FormatException("Edit order assignments on a plan without preassigned routes.");
        var orders = root["orders"]!["deliveries"]!.AsArray();
        foreach (var (id, value) in decisions)
        {
            var order = orders.OfType<JsonObject>().SingleOrDefault(o => RoutingInput.Text(o["id"]) == id)
                ?? throw new FormatException($"Order {id} is no longer in this plan.");
            var decision = RoutingInput.Text(value) ?? "";
            if (decision is Excluded or LeftOff) { orders.Remove(order); continue; }
            if (!decision.StartsWith("truck:", StringComparison.Ordinal)) throw new FormatException($"Invalid assignment for {id}.");
            var truckId = decision[6..];
            var truck = RoutingInput.Items(root["vehicles"]).SingleOrDefault(v => RoutingInput.Text(v["id"]) == truckId)
                ?? throw new FormatException($"Order {id} is pinned to unavailable truck {truckId}. Restore the truck or change the assignment.");
            PlannerOvertime.Allow(root, truck);
            var category = "AURORA_ORDER_PIN_" + Guid.NewGuid().ToString("N");
            truck["categories"] ??= new JsonArray();
            truck["categories"]!.AsArray().Add(category);
            order["properties"] ??= new JsonObject();
            order["properties"]!["categories"] ??= new JsonArray();
            order["properties"]!["categories"]!.AsArray().Add(category);
            root["constraints"] ??= new JsonObject();
            root["constraints"]!["combinations"] ??= new JsonObject();
            root["constraints"]!["combinations"]!["orderVehicle"] ??= new JsonArray();
            root["constraints"]!["combinations"]!["orderVehicle"]!.AsArray().Add(new JsonObject
            {
                ["type"] = "ORDER_REQUIRES_VEHICLE", ["orderCategory"] = category, ["vehicleCategory"] = category
            });
        }
        if (orders.Count == 0) throw new FormatException("Include at least one order before optimizing.");
        PlannerEdits.RemoveUnusedLocations(root);
        return root.ToJsonString();
    }
}
