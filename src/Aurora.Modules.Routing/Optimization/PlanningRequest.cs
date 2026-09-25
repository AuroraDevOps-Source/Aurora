using System.Text.Json.Nodes;
using Aurora.Contracts;

namespace Aurora.Modules.Routing.Optimization;

public static class PlanningRequest
{
    public static string Prepare(StartPlanningDto input)
    {
        if (input.DurationSeconds is < 5 or > 1200) throw new FormatException("Choose a search budget between 5 and 1200 seconds.");
        var prepared = RoutingInput.Prepare(input.RequestJson);
        if (input.KeepAssignments && input.PreviousResult is null) throw new FormatException("Keeping assignments needs a previous successful plan.");
        if (input.PreviousResult is not null) prepared = QuickUpdate.Prepare(prepared, input.PreviousResult);
        var root = RoutingInput.Parse(prepared);
        root["settings"] ??= new JsonObject();
        root["settings"]!["duration"] = input.DurationSeconds;
        if (input.KeepAssignments)
        {
            var orders = RoutingInput.Items(root["orders"]?["deliveries"]).ToDictionary(o => RoutingInput.Text(o["id"])!);
            var vehicles = RoutingInput.Items(root["vehicles"]).ToDictionary(v => RoutingInput.Text(v["id"])!);
            root["constraints"] ??= new JsonObject();
            root["constraints"]!["combinations"] ??= new JsonObject();
            root["constraints"]!["combinations"]!["orderVehicle"] ??= new JsonArray();
            var constraints = root["constraints"]!["combinations"]!["orderVehicle"]!.AsArray();
            foreach (var route in RoutingInput.Items(RoutingInput.Parse(input.PreviousResult!)["routes"]))
            {
                var id = RoutingInput.Text(route["vehicleId"])!;
                var assigned = RoutingInput.Items(route["stops"]).SelectMany(s => RoutingInput.Items(s["appointments"]))
                    .SelectMany(a => RoutingInput.Items(a["tasks"]))
                    .Where(t => RoutingInput.Text(t["type"]) == "DELIVERY")
                    .Select(t => RoutingInput.Text(t["orderId"])!).Distinct().Where(orders.ContainsKey).ToList();
                if (assigned.Count == 0) continue;
                if (!vehicles.TryGetValue(id, out var vehicle)) throw new FormatException($"Truck {id} has protected assignments. Restore it or turn off Keep assignments.");
                // Random category avoids collision with imported customer categories.
                var category = "AURORA_PIN_" + Guid.NewGuid().ToString("N");
                AddCategory(vehicle, category);
                foreach (var orderId in assigned)
                {
                    orders[orderId]["properties"] ??= new JsonObject();
                    AddCategory(orders[orderId]["properties"]!.AsObject(), category);
                }
                constraints.Add(new JsonObject { ["type"] = "ORDER_REQUIRES_VEHICLE", ["orderCategory"] = category, ["vehicleCategory"] = category });
            }
        }
        return root.ToJsonString();
    }
    private static void AddCategory(JsonObject node, string category)
    {
        node["categories"] ??= new JsonArray();
        node["categories"]!.AsArray().Add(category);
    }
}
