using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public static class PlannerLeftOff
{
    public static IReadOnlyList<DroppedOrderDto> Read(string json, OptimizationResultDto result)
    {
        var root = RoutingInput.Parse(json);
        var routed = result.Routes.SelectMany(r => r.Pros).Select(p => p.ProNumber).ToHashSet(StringComparer.Ordinal);
        var dropped = result.DroppedOrders.ToDictionary(o => o.OrderId, StringComparer.Ordinal);
        var rows = new List<DroppedOrderDto>();
        foreach (var order in RoutingInput.Items(root["orders"]?["deliveries"]))
        {
            var id = RoutingInput.Text(order["id"])!;
            var removed = PlanOrderDecisions.Get(root, id) == PlanOrderDecisions.Excluded;
            var held = PlanOrderDecisions.Get(root, id) == PlanOrderDecisions.LeftOff;
            if (!removed && routed.Contains(id)) continue;
            if (dropped.TryGetValue(id, out var existing))
            {
                rows.Add(removed ? existing with { Reason = "Removed by planner" } : held ? existing with { Reason = "Not added to a truck" } : existing);
                continue;
            }
            var report = RoutingInput.ReportOrder(root, id);
            var location = RoutingInput.Items(root["locations"]).FirstOrDefault(l => RoutingInput.Text(l["id"]) == RoutingInput.Text(order["delivery"]?["locationId"]));
            string Label(params string[] keys) => keys.Select(k => RoutingInput.Text(RoutingInput.Get(report, k))).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";
            double? Load(string dimension) => Number(RoutingInput.Items(order["properties"]?["loads"]).FirstOrDefault(l => RoutingInput.Text(l["dimension"]) == dimension)?["value"]);
            rows.Add(new(id, Number(location?["latitude"]), Number(location?["longitude"]), removed ? "Removed by planner" : held ? "Not added to a truck" : "Awaiting re-optimization",
                Label("shipToName", "carrierConsigneeName", "consigneeName", "name"), Label("address1", "address"), Label("city"), Label("state"), Label("postalCode"),
                Number(RoutingInput.Get(report, "units")), Number(RoutingInput.Get(report, "weight")) ?? Load("weight"), Number(RoutingInput.Get(report, "cubes")) ?? Load("volume"),
                Label("billType"), (int?)Number(order["delivery"]?["duration"]), RoutingInput.Describe(root, id)));
        }
        return rows.OrderBy(r => r.OrderId, StringComparer.Ordinal).ToList();
    }
    private static double? Number(JsonNode? value) => value is JsonValue v && v.TryGetValue<double>(out var n) ? n : null;
}
