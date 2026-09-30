using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public sealed record PlanningCustomer(string Key, string Name, string Destination, IReadOnlyList<string> OrderIds);

public static class PlanningCustomers
{
    private static string? Text(JsonObject? source, params string[] names) => names.Select(n => RoutingInput.Text(RoutingInput.Get(source, n))).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    public static IReadOnlyList<PlanningCustomer> Read(string json)
    {
        var root = RoutingInput.Parse(json);
        var locations = RoutingInput.Items(root["locations"]).ToDictionary(l => RoutingInput.Text(l["id"])!);
        return RoutingInput.Items(root["orders"]?["deliveries"]).Select(order =>
        {
            var id = RoutingInput.Text(order["id"])!;
            var report = RoutingInput.ReportOrder(root, id);
            var locationId = RoutingInput.Text(order["delivery"]?["locationId"]) ?? id;
            locations.TryGetValue(locationId, out var location);
            var name = Text(report, "shipToName", "carrierConsigneeName", "consigneeName", "name") ?? Text(location, "name") ?? "Delivery customer";
            var address = Text(report, "carrierConsigneeAddress", "carrierConsigneeAddress1", "shipToAddress", "shipToAddress1", "address1", "streetAddress", "street");
            var city = Text(report, "carrierConsigneeCity", "shipToCity", "city");
            var state = Text(report, "carrierConsigneeState", "shipToState", "state");
            var postal = Text(report, "carrierConsigneeZip", "shipToZip", "postalCode", "zip");
            var destination = string.Join(", ", new[] { address, city, state, postal }.Where(v => !string.IsNullOrWhiteSpace(v)));
            var coordinates = location?["latitude"] is not null && location?["longitude"] is not null ? $"{location["latitude"]},{location["longitude"]}" : null;
            // Names alone are not enough: separate different delivery destinations.
            var placeKey = address is not null ? destination : coordinates ?? locationId;
            var key = System.Text.Json.JsonSerializer.Serialize(new[] { name.Trim().ToUpperInvariant(), placeKey.Trim().ToUpperInvariant() });
            return new { Key = key, Name = name, Destination = destination.Length > 0 ? destination : coordinates ?? locationId, Id = id };
        }).GroupBy(x => x.Key).Select(g => new PlanningCustomer(g.Key, g.First().Name, g.First().Destination, g.Select(x => x.Id).ToArray())).ToArray();
    }
}
