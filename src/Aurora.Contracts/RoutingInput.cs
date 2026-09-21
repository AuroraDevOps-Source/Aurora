using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aurora.Contracts;

public sealed record AppointmentWindowDto(DateTimeOffset? EarliestStart, DateTimeOffset? LatestStart, DateTimeOffset? LatestEnd);
public sealed record AppointmentInfoDto(IReadOnlyList<AppointmentWindowDto> Windows, bool? Confirmed, string Source, bool Enforced);
public sealed record AppointmentImportResult(string Json, int Imported);

/// <summary>File-based appointment enrichment shared by the browser and API. Never reads live freight.</summary>
public static class RoutingInput
{
    public static JsonObject Parse(string json) => JsonNode.Parse(json) as JsonObject
        ?? throw new FormatException("The routing request must be a JSON object.");

    public static JsonNode? Get(JsonObject? value, string key) => value?
        .FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
    public static string? Text(JsonNode? value) => value is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
    public static IEnumerable<JsonObject> Items(JsonNode? node) => (node as JsonArray)?.OfType<JsonObject>() ?? [];

    public static JsonObject? ReportOrder(JsonObject root, string id)
    {
        var orders = root["reporting"]?["orders"];
        return orders is JsonObject keyed ? Get(keyed, id) as JsonObject : Items(orders)
            .FirstOrDefault(order => new[] { "id", "proNumber", "orderId" }.Any(key => Text(Get(order, key)) == id));
    }

    public static JsonObject? Appointment(JsonObject? report) => Get(report, "appointmentDetail") as JsonObject
        ?? Get(report, "appointment") as JsonObject;

    public static bool? Confirmed(JsonObject appointment)
    {
        var node = Get(appointment, "confirmed");
        if (node is null) return null;
        if (node is JsonValue v && v.TryGetValue<bool>(out var result)) return result;
        return ParseConfirmation(Text(node) ?? throw new FormatException("Appointment confirmation must be true or false."));
    }

    private static bool? ParseConfirmation(string value) => value.Trim().ToLowerInvariant() switch
    {
        "" => null, "true" or "yes" or "1" => true, "false" or "no" or "0" => false,
        _ => throw new FormatException("Appointment confirmation must be true/false, yes/no or 1/0.")
    };

    public static DateTimeOffset? Time(JsonNode? node)
    {
        var text = Text(node);
        if (string.IsNullOrWhiteSpace(text)) return null;
        // A missing offset must never silently adopt the browser/server's local timezone.
        if (!Regex.IsMatch(text, @"T.*(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw new FormatException($"Use an ISO date/time with an explicit offset, such as 2026-09-14T09:00:00-04:00 (received '{text}').");
        return value.Year <= 1 ? null : value;
    }

    private static (DateTimeOffset? Start, DateTimeOffset? End) Bounds(JsonObject appointment)
    {
        var start = Time(Get(appointment, "blockBegin") ?? Get(appointment, "start"));
        var end = Time(Get(appointment, "blockEnd") ?? Get(appointment, "end"));
        if ((start is null) != (end is null) || (start is not null && end < start))
            throw new FormatException("An appointment needs both start and end, with end at or after start.");
        if (Confirmed(appointment) == true && start is null)
            throw new FormatException("A confirmed appointment needs a dated start and end.");
        return (start, end);
    }

    /// <summary>
    /// Restrict confirmed appointments using PTV location time slots + task timeSlotIds.
    /// Clone the location per order so another order at the same address is not constrained accidentally.
    /// Intersect existing windows and retain preparation/cost/other slot properties, never widen them.
    /// </summary>
    public static string Prepare(string json)
    {
        var root = Parse(json);
        var locations = root["locations"] as JsonArray ?? throw new FormatException("The request needs a locations array.");
        var deliveries = root["orders"]?["deliveries"] as JsonArray ?? throw new FormatException("This workspace currently requires delivery orders.");
        foreach (var order in deliveries.OfType<JsonObject>())
        {
            var id = Text(order["id"]) ?? throw new FormatException("Every delivery needs an ID.");
            var report = ReportOrder(root, id);
            var appointment = Appointment(report);
            if (appointment is null) continue;
            var (start, end) = Bounds(appointment);
            if (Confirmed(appointment) != true) continue;
            if (root["routes"] is JsonArray { Count: > 0 })
                throw new FormatException("Imported confirmed appointments cannot be applied to preassigned routes. Use a fresh delivery request so route/location restrictions are not silently changed.");
            var delivery = order["delivery"] as JsonObject ?? throw new FormatException($"Delivery {id} has no task.");
            var locationId = Text(delivery["locationId"]);
            var location = locations.OfType<JsonObject>().FirstOrDefault(x => Text(x["id"]) == locationId)
                ?? throw new FormatException($"Delivery {id} has an unknown location.");
            var clone = (JsonObject)location.DeepClone();
            var cloneId = "AURORA_APPT_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..24];
            // Re-preparing an already prepared request must not duplicate locations or slots.
            if (locationId != cloneId && locations.OfType<JsonObject>().Any(x => Text(x["id"]) == cloneId))
                throw new FormatException($"Duplicate appointment location for order {id}.");
            var properties = clone["stopProperties"] as JsonObject;
            if (properties is null) clone["stopProperties"] = properties = new JsonObject();
            var available = Items(properties["timeSlots"]).ToList();
            var selected = (delivery["timeSlotIds"] as JsonArray)?.Select(Text).ToHashSet(StringComparer.Ordinal);
            if (selected is { Count: > 0 }) available = available.Where(slot => selected.Contains(Text(slot["id"]))).ToList();
            if (available.Count == 0 && properties["timeSlots"] is null && selected is not { Count: > 0 })
                available.Add(new JsonObject()); // An originally unrestricted location.
            var restricted = new JsonArray();
            foreach (var original in available)
            {
                var earliest = Later(Time(original["earliestStart"]), start);
                var latest = Earlier(Time(original["latestStart"]), end);
                var latestEnd = Time(original["latestEnd"]);
                if (latest < earliest || latestEnd < earliest) continue;
                var slot = (JsonObject)original.DeepClone();
                slot["id"] = "APPT_" + restricted.Count;
                slot["earliestStart"] = earliest!.Value.ToString("O");
                slot["latestStart"] = latest!.Value.ToString("O");
                restricted.Add(slot);
            }
            if (restricted.Count == 0)
                throw new FormatException($"Confirmed appointment for {id} conflicts with its existing location/time-slot restrictions. Correct the test data before optimizing.");
            clone["id"] = cloneId;
            properties["timeSlots"] = restricted;
            if (locationId == cloneId) locations[locations.IndexOf(location)] = clone;
            else locations.Add(clone);
            // Location-level display fields must follow the isolated location as well.
            var locationReports = root["reporting"]?["locations"];
            if (locationId != cloneId && locationId is not null)
            {
                var sourceReport = locationReports is JsonObject keyed ? Get(keyed, locationId) as JsonObject
                    : Items(locationReports).FirstOrDefault(x => Text(Get(x, "id")) == locationId || Text(Get(x, "locationId")) == locationId);
                if (sourceReport is not null)
                {
                    var clonedReport = (JsonObject)sourceReport.DeepClone();
                    if (locationReports is JsonObject keyedReports) keyedReports[cloneId] = clonedReport;
                    else if (locationReports is JsonArray arrayReports)
                    {
                        foreach (var key in clonedReport.Select(p => p.Key).Where(k => k.Equals("id", StringComparison.OrdinalIgnoreCase) || k.Equals("locationId", StringComparison.OrdinalIgnoreCase)).ToList()) clonedReport.Remove(key);
                        clonedReport["id"] = cloneId;
                        clonedReport["locationId"] = cloneId;
                        arrayReports.Add(clonedReport);
                    }
                }
            }
            delivery["locationId"] = cloneId;
            delivery["timeSlotIds"] = new JsonArray(restricted.OfType<JsonObject>().Select(s => (JsonNode?)JsonValue.Create(Text(s["id"]))).ToArray());
        }
        return root.ToJsonString();
    }

    private static DateTimeOffset? Later(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a > b ? a : b;
    private static DateTimeOffset? Earlier(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a < b ? a : b;

    public static AppointmentInfoDto? Describe(JsonObject root, string orderId)
    {
        var order = Items(root["orders"]?["deliveries"]).FirstOrDefault(x => Text(x["id"]) == orderId);
        var appointment = Appointment(ReportOrder(root, orderId));
        var delivery = order?["delivery"] as JsonObject;
        var location = Items(root["locations"]).FirstOrDefault(x => Text(x["id"]) == Text(delivery?["locationId"]));
        var slots = Items(location?["stopProperties"]?["timeSlots"]);
        if (delivery?["timeSlotIds"] is JsonArray ids && ids.Count > 0)
            slots = slots.Where(s => ids.Select(Text).Contains(Text(s["id"])));
        var windows = slots.Select(s => new AppointmentWindowDto(Time(s["earliestStart"]), Time(s["latestStart"]), Time(s["latestEnd"]))).ToList();
        if (appointment is not null)
        {
            var (start, end) = Bounds(appointment);
            if (start is null) return windows.Count > 0 ? new(windows, null, "PTV time slots", true) : null;
            var confirmed = Confirmed(appointment);
            // Preserve the customer's appointment separately from any tighter opening-hours limits.
            return new([new(start, end, null)], confirmed, "Imported appointment", confirmed == true);
        }
        return windows.Count > 0 ? new(windows, null, "PTV time slots", true) : null;
    }

    public static string AppointmentTemplate(string json)
    {
        var root = Parse(json);
        var csv = new StringBuilder("PRO,AppointmentStart,AppointmentEnd,AppointmentConfirmed\r\n");
        foreach (var order in Items(root["orders"]?["deliveries"]))
        {
            var id = Text(order["id"]) ?? "";
            var appointment = Appointment(ReportOrder(root, id));
            var bounds = appointment is null ? default : Bounds(appointment);
            csv.AppendLine(string.Join(",", new[] { Csv(id), Csv(bounds.Start?.ToString("O")), Csv(bounds.End?.ToString("O")), appointment is null ? "" : Confirmed(appointment)?.ToString() ?? "" }));
        }
        return csv.ToString();
    }

    public static AppointmentImportResult ImportCsv(string json, string csv)
    {
        var root = Parse(json);
        var rows = ParseCsv(csv.TrimStart('\uFEFF'));
        if (rows.Count < 2) throw new FormatException("The appointment CSV needs a header and at least one row.");
        var header = rows[0].Select(x => x.Trim().ToLowerInvariant()).ToList();
        int Column(params string[] aliases) => header.FindIndex(x => aliases.Contains(x));
        var proCol = Column("pro", "pronumber", "orderid");
        var startCol = Column("appointmentstart", "blockbegin", "appointmentdetail.blockbegin");
        var endCol = Column("appointmentend", "blockend", "appointmentdetail.blockend");
        var confirmedCol = Column("appointmentconfirmed", "confirmed", "appointmentdetail.confirmed");
        if (new[] { proCol, startCol, endCol, confirmedCol }.Any(i => i < 0))
            throw new FormatException("CSV columns must include PRO, AppointmentStart, AppointmentEnd and AppointmentConfirmed. Download the template for this file.");
        var ids = Items(root["orders"]?["deliveries"]).Select(x => Text(x["id"])).ToHashSet(StringComparer.Ordinal);
        if (root["reporting"] is not JsonObject) root["reporting"] = new JsonObject();
        var reports = root["reporting"]!["orders"];
        if (reports is null) root["reporting"]!["orders"] = reports = new JsonObject();
        if (reports is not JsonObject && reports is not JsonArray) throw new FormatException("reporting.orders must be an object or array.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Count != header.Count) throw new FormatException("A CSV row has a different number of columns than its header.");
            var id = row[proCol].Trim();
            if (!ids.Contains(id)) throw new FormatException($"CSV PRO '{id}' is not in the selected JSON file.");
            if (!seen.Add(id)) throw new FormatException($"CSV PRO '{id}' is duplicated.");
            if (string.IsNullOrWhiteSpace(row[startCol]) && string.IsNullOrWhiteSpace(row[endCol]) && string.IsNullOrWhiteSpace(row[confirmedCol])) continue;
            var appointment = new JsonObject { ["blockBegin"] = row[startCol].Trim(), ["blockEnd"] = row[endCol].Trim(), ["confirmed"] = ParseConfirmation(row[confirmedCol]) };
            _ = Bounds(appointment);
            var report = ReportOrder(root, id);
            if (report is null)
            {
                report = new JsonObject { ["id"] = id };
                if (reports is JsonObject keyed) keyed[id] = report; else ((JsonArray)reports).Add(report);
            }
            foreach (var key in report.Select(x => x.Key).Where(k => k.Equals("appointment", StringComparison.OrdinalIgnoreCase) || k.Equals("appointmentDetail", StringComparison.OrdinalIgnoreCase)).ToList()) report.Remove(key);
            report["appointment"] = appointment;
            count++;
        }
        var output = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        _ = Prepare(output); // Validate all conflicts atomically, without changing the caller's original file.
        return new(output, count);
    }

    public static string Csv(string? value)
    {
        value ??= "";
        // Protect exported labels/PROs against spreadsheet formula execution.
        var formulaPrefix = value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@');
        // Plain negative decimals (e.g. longitude) are safe numeric data, not formulas.
        if (formulaPrefix && !Regex.IsMatch(value, @"^-\d+(?:\.\d+)?$")) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closed = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') { quoted = false; closed = true; }
                else field.Append(c);
            }
            else if (c == ',' || c is '\r' or '\n')
            {
                row.Add(field.ToString()); field.Clear(); closed = false;
                if (c != ',') { rows.Add(row); row = []; if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
            }
            else if (c == '"' && field.Length == 0 && !closed) quoted = true;
            else if (closed || c == '"') throw new FormatException("Malformed quoting in appointment CSV.");
            else field.Append(c);
        }
        if (quoted) throw new FormatException("Unclosed quoted field in appointment CSV.");
        if (field.Length > 0 || row.Count > 0 || closed) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
