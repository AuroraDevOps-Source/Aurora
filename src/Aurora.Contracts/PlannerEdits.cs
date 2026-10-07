using System.Globalization;
using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public static class PlannerEdits
{
    // Puts every truck on one ship date with the same local hours, keeping each truck's own UTC offset.
    // A return at or before departure is an overnight shift, as with saved truck shifts.
    // PTV validates every listed location, so drop the ones no depot, truck, or order refers to.
    public static void RemoveUnusedLocations(JsonObject root)
    {
        if (root["locations"] is not JsonArray locations) return;
        var used = new HashSet<string>(StringComparer.Ordinal);
        void Collect(JsonNode? node)
        {
            if (node is JsonObject obj)
                foreach (var (key, value) in obj)
                {
                    if (key == "locationId" && RoutingInput.Text(value) is { } id) used.Add(id);
                    else Collect(value);
                }
            else if (node is JsonArray array) foreach (var item in array) Collect(item);
        }
        foreach (var (key, value) in root) if (key != "locations") Collect(value);
        for (var i = locations.Count - 1; i >= 0; i--)
            if (RoutingInput.Text(locations[i]?["id"]) is not { } id || !used.Contains(id)) locations.RemoveAt(i);
    }

    // PTV rejects AVERAGE (time-of-day) traffic when two locations are more than 650 km apart. Plans
    // that span farther, such as line haul across regions, use CONSTANT travel times instead.
    public const double AverageTrafficLimitKm = 640; // a small margin under PTV's 650 km
    public static string FitTrafficMode(string json)
    {
        var root = RoutingInput.Parse(json);
        RemoveUnusedLocations(root);
        var points = RoutingInput.Items(root["locations"])
            .Select(l => (Lat: Number(l["latitude"]), Lon: Number(l["longitude"])))
            .Where(p => p.Lat is not null && p.Lon is not null).Select(p => (p.Lat!.Value, p.Lon!.Value)).ToArray();
        var longHaul = false;
        for (var i = 0; i < points.Length && !longHaul; i++)
            for (var j = i + 1; j < points.Length && !longHaul; j++)
                longHaul = Kilometers(points[i], points[j]) > AverageTrafficLimitKm;
        if (longHaul)
            foreach (var truck in RoutingInput.Items(root["vehicles"]).Concat(RoutingInput.Items(root["reporting"]?["unavailableVehicles"])))
                if (truck["routing"] is JsonObject routing && RoutingInput.Text(routing["trafficMode"]) == "AVERAGE") routing["trafficMode"] = "CONSTANT";
        return root.ToJsonString();
    }
    private static double? Number(JsonNode? node) => node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;
    private static double Kilometers((double Lat, double Lon) a, (double Lat, double Lon) b)
    {
        double Rad(double degrees) => degrees * Math.PI / 180;
        var h = Math.Pow(Math.Sin(Rad(b.Lat - a.Lat) / 2), 2) + Math.Cos(Rad(a.Lat)) * Math.Cos(Rad(b.Lat)) * Math.Pow(Math.Sin(Rad(b.Lon - a.Lon) / 2), 2);
        return 2 * 6371 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static string FleetDay(string json, DateOnly date, string departAt, string returnBy)
    {
        if (!TimeOnly.TryParseExact(departAt, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var depart) ||
            !TimeOnly.TryParseExact(returnBy, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var back))
            throw new FormatException("Enter truck departure and return times as HH:mm.");
        if (depart == back) throw new FormatException("Trucks must return at a different time than they depart.");
        var root = RoutingInput.Parse(json);
        var trucks = RoutingInput.Items(root["vehicles"]).Concat(RoutingInput.Items(root["reporting"]?["unavailableVehicles"])).ToArray();
        if (trucks.Length == 0) throw new FormatException("There are no trucks in this plan.");
        foreach (var truck in trucks)
        {
            var offset = RoutingInput.Time(truck["start"]?["earliestStartTime"])?.Offset
                ?? throw new FormatException($"Truck {RoutingInput.Text(truck["id"])} has no dated shift to take its UTC offset from.");
            var start = new DateTimeOffset(date.ToDateTime(depart), offset);
            var end = new DateTimeOffset((back > depart ? date : date.AddDays(1)).ToDateTime(back), offset);
            truck["start"]!["earliestStartTime"] = start.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
            truck["end"]!["latestEndTime"] = end.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        }
        return root.ToJsonString();
    }

    public static string CustomerAppointment(string json, string customerKey, string? start, string? end, bool confirmed, double? serviceMinutes, bool clear, bool replaceNativeWindows = false)
    {
        var customer = PlanningCustomers.Read(json).SingleOrDefault(c => c.Key == customerKey)
            ?? throw new FormatException("Choose a customer from this plan.");
        foreach (var id in customer.OrderIds)
            json = Appointment(json, id, start, end, confirmed, serviceMinutes, clear, replaceNativeWindows);
        return json;
    }

    public static string Appointment(string json, string id, string? start, string? end, bool confirmed, double? serviceMinutes, bool clear, bool replaceNativeWindows = false)
    {
        var root = RoutingInput.Parse(json);
        var order = RoutingInput.Items(root["orders"]?["deliveries"]).SingleOrDefault(x => RoutingInput.Text(x["id"]) == id)
            ?? throw new FormatException("Choose an order from this plan.");
        if (replaceNativeWindows)
        {
            if (root["routes"] is JsonArray { Count: > 0 }) throw new FormatException("Edit appointments on a fresh delivery request without preassigned routes.");
            var delivery = order["delivery"] as JsonObject ?? throw new FormatException("This order has no delivery task.");
            var locations = root["locations"] as JsonArray ?? throw new FormatException("The plan needs locations.");
            var oldId = RoutingInput.Text(delivery["locationId"]);
            var location = locations.OfType<JsonObject>().SingleOrDefault(x => RoutingInput.Text(x["id"]) == oldId) ?? throw new FormatException("The order's location was not found.");
            var clone = (JsonObject)location.DeepClone();
            var newId = "EDIT_" + Guid.NewGuid().ToString("N");
            clone["id"] = newId;
            if (clone["stopProperties"] is JsonObject props) props.Remove("timeSlots");
            locations.Add(clone);
            delivery["locationId"] = newId;
            delivery.Remove("timeSlotIds");
            // Copy location display metadata to retain the address after isolating the order.
            var reports = root["reporting"]?["locations"];
            if (reports is JsonObject keyed && oldId is not null && RoutingInput.Get(keyed, oldId) is { } report)
                keyed[newId] = report.DeepClone();
            else if (reports is JsonArray array)
            {
                var locationReport = array.OfType<JsonObject>().FirstOrDefault(x => RoutingInput.Text(RoutingInput.Get(x, "id") ?? RoutingInput.Get(x, "locationId")) == oldId);
                if (locationReport is not null)
                {
                    var copied = (JsonObject)locationReport.DeepClone();
                    foreach (var key in copied.Select(x => x.Key).Where(x => x.Equals("id", StringComparison.OrdinalIgnoreCase) || x.Equals("locationId", StringComparison.OrdinalIgnoreCase)).ToArray()) copied.Remove(key);
                    copied["id"] = newId; array.Add(copied);
                }
            }
        }
        if (serviceMinutes is not null)
        {
            if (!double.IsFinite(serviceMinutes.Value) || serviceMinutes < 0 || serviceMinutes > 1440) throw new FormatException("Service time must be between 0 and 1,440 minutes.");
            order["delivery"]!["duration"] = (int)Math.Round(serviceMinutes.Value * 60);
        }
        if (clear)
        {
            var report = RoutingInput.ReportOrder(root, id);
            if (report is not null)
                foreach (var key in report.Select(x => x.Key).Where(x => x.Equals("appointment", StringComparison.OrdinalIgnoreCase) || x.Equals("appointmentDetail", StringComparison.OrdinalIgnoreCase)).ToArray()) report.Remove(key);
            // Native opening hours and PTV slots are intentionally retained.
            var result = root.ToJsonString();
            _ = RoutingInput.Prepare(result);
            return result;
        }
        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end) && !confirmed)
        {
            var result = root.ToJsonString(); _ = RoutingInput.Prepare(result); return result;
        }
        string Quote(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        return RoutingInput.ImportCsv(root.ToJsonString(), "PRO,AppointmentStart,AppointmentEnd,AppointmentConfirmed\n" +
            string.Join(',', Quote(id), Quote(start), Quote(end), confirmed ? "true" : "false")).Json;
    }
}
