using Aurora.Contracts;
using System.Text.Json.Nodes;

namespace Aurora.Client;

/// <summary>A preview of imported inputs, not an optimized route or a saved dispatch plan.</summary>
public sealed record RoutingDraft(int Orders, int Trucks, int Windows, int InformationalAppointments,
    string Terminal, IReadOnlyList<RoutingDraftOrder> Deliveries, IReadOnlyList<RoutingPreviewPoint> Points)
{
    public static RoutingDraft Read(string json)
    {
        var root = RoutingInput.Parse(RoutingInput.Prepare(json));
        var locations = RoutingInput.Items(root["locations"]).Where(l => RoutingInput.Text(l["id"]) is not null)
            .ToDictionary(l => RoutingInput.Text(l["id"])!, StringComparer.Ordinal);
        var deliveries = new List<RoutingDraftOrder>();
        var points = new List<RoutingPreviewPoint>();
        foreach (var order in RoutingInput.Items(root["orders"]?["deliveries"]))
        {
            var id = RoutingInput.Text(order["id"])!;
            locations.TryGetValue(RoutingInput.Text(order["delivery"]?["locationId"]) ?? "", out var location);
            var report = RoutingInput.ReportOrder(root, id);
            var name = Label(report, "shipToName", "carrierConsigneeName", "consigneeName", "name")
                ?? Label(location, "name") ?? "Delivery location";
            var appointment = RoutingInput.Describe(root, id);
            var point = Point(location, id, name, false);
            deliveries.Add(new(id, name, appointment, point is not null));
            if (point is not null) points.Add(point);
        }
        var vehicles = RoutingInput.Items(root["vehicles"]).ToList();
        var depotIds = RoutingInput.Items(root["depots"]).Select(d => RoutingInput.Text(d["locationId"]))
            .Concat(vehicles.Select(v => RoutingInput.Text(v["start"]?["locationId"])))
            .Where(id => id is not null).Distinct(StringComparer.Ordinal);
        foreach (var id in depotIds)
            if (locations.TryGetValue(id!, out var location) && Point(location, id!, "Depot", true) is { } point)
                points.Add(point);
        var terminal = Label(root["reporting"] as JsonObject, "terminal", "activeTerminal", "originTerminal")
            ?? Label(root, "terminal", "activeTerminal", "originTerminal") ?? "Terminal not provided";
        return new(deliveries.Count, vehicles.Count, deliveries.Count(d => d.Appointment?.Enforced == true),
            deliveries.Count(d => d.Appointment is { Enforced: false }), terminal, deliveries, points);
    }

    private static string? Label(JsonObject? obj, params string[] names) => names
        .Select(n => RoutingInput.Text(RoutingInput.Get(obj, n))).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

    private static RoutingPreviewPoint? Point(JsonObject? location, string id, string label, bool depot)
    {
        if (location?["latitude"] is not JsonValue latValue || !latValue.TryGetValue<double>(out var latitude) ||
            location["longitude"] is not JsonValue lonValue || !lonValue.TryGetValue<double>(out var longitude) ||
            !double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return null;
        return new(latitude, longitude, id, label, depot);
    }
}

public sealed record RoutingDraftOrder(string Id, string Name, AppointmentInfoDto? Appointment, bool HasCoordinates);
public sealed record RoutingPreviewPoint(double Latitude, double Longitude, string Id, string Label, bool IsDepot);
