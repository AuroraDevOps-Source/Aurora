using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace Aurora.Modules.Routing.Optimization;

/// <summary>
/// The routing knobs PTV's OptiFlow request exposes, lifted out of the JSON so they can be
/// edited on a form and written back. Reads the first vehicle as the fleet template: everything
/// here applies to every truck, which is what you want for "same fleet, different size".
///
/// Anything not modelled here (per-order time slots, extra capacity dimensions, breaks) is left
/// untouched on write-back.
/// </summary>
public sealed class FleetSettings
{
    /// <summary>Profiles confirmed to work on this API key by probing PTV directly (2026-08-05).
    /// The list is open — PTV looks the name up rather than validating an enum — so the combo is
    /// editable. USA_5_DELIVERY is the only US profile that resolved.</summary>
    public static readonly string[] KnownProfiles =
        ["USA_5_DELIVERY", "EUR_TRAILER_TRUCK", "EUR_VAN", "EUR_CAR"];

    /// <summary>PTV rejects anything else with GENERAL_ENUM_VIOLATED.</summary>
    public static readonly string[] TrafficModes = ["AVERAGE", "CONSTANT"];

    public int TruckCount { get; set; } = 1;
    public double MaxWeight { get; set; }
    public double MaxVolume { get; set; }
    public double CostPerHour { get; set; }
    public double CostPerKm { get; set; }
    public double CostPerStop { get; set; }
    public double CostFixed { get; set; }
    public string Profile { get; set; } = "USA_5_DELIVERY";
    public string TrafficMode { get; set; } = "AVERAGE";
    public string ShiftDate { get; set; } = "";
    public string ShiftStart { get; set; } = "06:00:00";
    public string ShiftEnd { get; set; } = "20:00:00";
    public string UtcOffset { get; set; } = "-07:00";
    public int CalcSeconds { get; set; } = 30;
    public int ServiceMinutes { get; set; } = 15;

    private static readonly Regex Stamp =
        new(@"^(?<date>\d{4}-\d{2}-\d{2})T(?<time>\d{2}:\d{2}(:\d{2})?)(?<offset>Z|[+-]\d{2}:\d{2})?$",
            RegexOptions.Compiled);

    public static FleetSettings ReadFrom(JsonObject request)
    {
        var s = new FleetSettings();

        var vehicles = request["vehicles"] as JsonArray;
        s.TruckCount = vehicles?.Count ?? 0;

        if (vehicles?.FirstOrDefault() is JsonObject v)
        {
            s.MaxWeight = Load(v, "weight");
            s.MaxVolume = Load(v, "volume");

            var costs = v["costs"];
            s.CostPerHour = Dbl(costs?["perHour"]);
            s.CostPerKm = Dbl(costs?["perKilometer"]);
            s.CostPerStop = Dbl(costs?["perStop"]);
            s.CostFixed = Dbl(costs?["fixed"]);

            s.Profile = Str(v["routing"]?["profile"]) ?? s.Profile;
            s.TrafficMode = Str(v["routing"]?["trafficMode"]) ?? s.TrafficMode;

            if (Split(Str(v["start"]?["earliestStartTime"])) is var (d, t, o) && d is not null)
            {
                s.ShiftDate = d;
                s.ShiftStart = t!;
                s.UtcOffset = o ?? s.UtcOffset;
            }
            if (Split(Str(v["end"]?["latestEndTime"])).Time is string endTime)
                s.ShiftEnd = endTime;
        }

        if (request["settings"]?["duration"] is JsonNode dur)
            s.CalcSeconds = (int)Dbl(dur);

        // Service time is per order; show the first one's, since the form sets them all.
        if ((request["orders"]?["deliveries"] as JsonArray)?.FirstOrDefault() is JsonObject first &&
            first["delivery"]?["duration"] is JsonNode sd)
            s.ServiceMinutes = Math.Max(0, (int)Dbl(sd) / 60);

        return s;
    }

    /// <summary>Writes these values into the request. Returns a description of what changed.</summary>
    public List<string> ApplyTo(JsonObject request)
    {
        var changes = new List<string>();

        request["settings"] ??= new JsonObject();
        request["settings"]!["duration"] = CalcSeconds;
        changes.Add($"calc budget {CalcSeconds}s");

        var deliveries = request["orders"]?["deliveries"] as JsonArray;
        if (deliveries is not null)
        {
            foreach (var d in deliveries)
                if (d?["delivery"] is JsonObject task)
                    task["duration"] = ServiceMinutes * 60;
            changes.Add($"service {ServiceMinutes} min x {deliveries.Count} orders");
        }

        var existing = (request["vehicles"] as JsonArray)?.Where(v => v is not null).Select(v => v!).ToList() ?? [];
        if (existing.Count == 0)
            throw new InvalidOperationException("The request has no vehicles to use as a fleet template.");

        var template = existing[0];
        var ids = existing.Select(v => Str(v["id"]) ?? "").ToList();
        var before = existing.Count;

        // Grow by cloning the template, shrink by dropping from the end — so the trucks the file
        // came with keep their identity (real trailer names, for instance).
        var fleet = new JsonArray();
        var taken = new HashSet<string>(ids, StringComparer.Ordinal);
        var next = 0;

        for (var i = 0; i < TruckCount; i++)
        {
            var node = (i < existing.Count ? existing[i] : template).DeepClone();

            if (i >= existing.Count)
            {
                string id;
                do { id = $"TRUCK_{++next}"; } while (!taken.Add(id));
                node["id"] = id;
            }

            Write(node);
            fleet.Add(node);
        }

        request["vehicles"] = fleet;

        changes.Add(before == TruckCount
            ? $"{TruckCount} trucks (unchanged count)"
            : $"trucks {before} -> {TruckCount}");
        changes.Add($"capacity {MaxWeight:N0} weight / {MaxVolume:N0} volume");
        changes.Add($"costs {CostPerHour:N2}/h + {CostPerKm:N2}/km + {CostPerStop:N2}/stop + {CostFixed:N2} fixed");
        changes.Add($"routing {Profile} / {TrafficMode}");
        changes.Add($"shift {ShiftDate} {ShiftStart} -> {ShiftEnd} ({UtcOffset})");

        return changes;
    }

    private void Write(JsonNode vehicle)
    {
        vehicle["routing"] ??= new JsonObject();
        vehicle["routing"]!["profile"] = Profile;
        vehicle["routing"]!["trafficMode"] = TrafficMode;

        vehicle["costs"] ??= new JsonObject();
        vehicle["costs"]!["perHour"] = CostPerHour;
        vehicle["costs"]!["perKilometer"] = CostPerKm;
        vehicle["costs"]!["perStop"] = CostPerStop;
        vehicle["costs"]!["fixed"] = CostFixed;

        vehicle["start"] ??= new JsonObject();
        vehicle["start"]!["earliestStartTime"] = $"{ShiftDate}T{Time(ShiftStart)}{UtcOffset}";
        vehicle["end"] ??= new JsonObject();
        vehicle["end"]!["latestEndTime"] = $"{ShiftDate}T{Time(ShiftEnd)}{UtcOffset}";

        // Update weight/volume in place and leave any other dimension the file carried alone.
        var loads = vehicle["constraints"]?["maximumLoads"] as JsonArray;
        if (loads is null)
        {
            vehicle["constraints"] ??= new JsonObject();
            vehicle["constraints"]!["maximumLoads"] = loads = new JsonArray();
        }

        SetLoad(loads, "weight", MaxWeight);
        SetLoad(loads, "volume", MaxVolume);
    }

    private static void SetLoad(JsonArray loads, string dimension, double value)
    {
        foreach (var l in loads)
        {
            if (string.Equals(Str(l?["dimension"]), dimension, StringComparison.OrdinalIgnoreCase))
            {
                l!["value"] = value;
                return;
            }
        }
        loads.Add(new JsonObject { ["dimension"] = dimension, ["value"] = value });
    }

    private static double Load(JsonObject vehicle, string dimension)
    {
        foreach (var l in vehicle["constraints"]?["maximumLoads"] as JsonArray ?? [])
            if (string.Equals(Str(l?["dimension"]), dimension, StringComparison.OrdinalIgnoreCase))
                return Dbl(l!["value"]);
        return 0;
    }

    /// <summary>Accepts "06:00" or "06:00:00" and normalises to seconds precision.</summary>
    private static string Time(string value) =>
        value.Count(c => c == ':') == 1 ? value + ":00" : value;

    private static (string? Date, string? Time, string? Offset) Split(string? stamp)
    {
        if (stamp is null) return (null, null, null);
        var m = Stamp.Match(stamp);
        return m.Success
            ? (m.Groups["date"].Value, Time(m.Groups["time"].Value),
               m.Groups["offset"].Success ? m.Groups["offset"].Value : null)
            : (null, null, null);
    }

    private static string? Str(JsonNode? n)
    {
        try { return n?.GetValue<string>(); }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Reads a JSON number. Goes via the raw JSON text rather than GetValue&lt;double&gt;(), because a
    /// node this class wrote itself holds a boxed int and GetValue&lt;double&gt;() throws on it — which
    /// silently zeroed values on the read-apply-read round trip.
    /// </summary>
    private static double Dbl(JsonNode? n) =>
        n is not null &&
        double.TryParse(n.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : 0;

    public static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
