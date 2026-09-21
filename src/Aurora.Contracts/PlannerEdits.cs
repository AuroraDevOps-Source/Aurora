using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public static class PlannerEdits
{
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
