using System.Globalization;
using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public static class PlannerOvertime
{
    public const string ShiftKey = "manualAssignmentShifts";

    public static void Allow(JsonObject root, JsonObject truck)
    {
        var id = RoutingInput.Text(truck["id"])!;
        root["reporting"] ??= new JsonObject();
        root["reporting"]![ShiftKey] ??= new JsonObject();
        var shifts = root["reporting"]![ShiftKey]!.AsObject();
        // Prepared copies may contain several manual orders on the same truck.
        if (shifts.ContainsKey(id)) return;
        var end = RoutingInput.Time(truck["end"]?["latestEndTime"])
            ?? throw new FormatException($"Truck {id} needs a planned return time before allowing overtime.");
        shifts[id] = truck.DeepClone();
        truck["end"]!["latestEndTime"] = end.AddDays(1).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        // Relax the planned work duration, not driving limits, breaks or appointments.
        (truck["constraints"]?["route"] as JsonObject)?.Remove("maximumDuration");
    }

    public static int OverShiftMinutes(string json, RouteSummaryDto route)
    {
        var root = RoutingInput.Parse(json);
        var truck = root["reporting"]?[ShiftKey]?[route.Vehicle] as JsonObject
            ?? RoutingInput.Items(root["vehicles"]).FirstOrDefault(v => RoutingInput.Text(v["id"]) == route.Vehicle);
        var end = RoutingInput.Time(truck?["end"]?["latestEndTime"]);
        var seconds = route.BudgetedSeconds is { } budget ? Math.Max(0, route.EstimatedSeconds - budget) : 0d;
        if (end is { } planned && route.Return is { } actual)
            seconds = Math.Max(seconds, (actual - planned).TotalSeconds);
        if (truck?["constraints"]?["route"]?["maximumDuration"] is JsonValue value && value.TryGetValue<int>(out var maximum))
            seconds = Math.Max(seconds, route.EstimatedSeconds - maximum);
        return (int)Math.Ceiling(Math.Max(0, seconds) / 60);
    }
}
