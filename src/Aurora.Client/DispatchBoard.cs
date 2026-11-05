using System.Net.Http.Json;
using Aurora.Contracts;

namespace Aurora.Client;

public sealed record DispatchItem(string Key, string Number, string Description, string Terminal,
    DateTime? Scheduled, DateTime? Due, string Zone, string Status, string Lane, string Url,
    string Driver = "", string Truck = "", int Orders = 0, double? Latitude = null,
    double? Longitude = null, string[]? Blockers = null)
{
    public DispatchStop[] Stops { get; init; } = [];
    public long? DriverId { get; init; }
    public string[] Issues => Blockers ?? [];
    public bool Mappable => DataRequirements.HasCoordinates(Latitude, Longitude);
    public bool Late(DateTimeOffset now) => Lane != "Complete" && Due is {} due && due < DispatchBoardRules.Clock(now, Zone);
}
public sealed record DispatchStop(string Number, string Name, double Latitude, double Longitude, int Sequence);

public static class DispatchBoardRules
{
    public static bool NativeOrderClosed(NovaRecordSummary order) => order.Completed ||
        order.StatusCode.Trim().ToUpperInvariant() is "CPLT" or "DONE" or "VOID" ||
        new[]{"Delivered","Closed","Complete","Completed","Void","Voided"}.Contains(order.Status,StringComparer.OrdinalIgnoreCase);
    public static string[] NativeOrderIssues(NovaRecordSummary order) => order.Issues.Select(i=>i.Message)
        .Concat(string.IsNullOrWhiteSpace(order.StatusCode) ? new[]{"Unknown Nova status; review the order before dispatch."} : [])
        .Distinct().ToArray();
    public static DateTime Clock(DateTimeOffset now, string zone) => string.IsNullOrEmpty(zone)
        ? now.LocalDateTime : TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime;
    public static bool InScope(DispatchItem item, DateTime date, bool carryOver) =>
        item.Scheduled is not {} scheduled || scheduled.Date == date.Date ||
        carryOver && scheduled.Date < date.Date && item.Lane != "Complete";
    public static string LocalLane(string status) => status switch
    {
        "Complete" => "Complete",
        "Dispatched" or "En Route" or "Arrived" => "Dispatched",
        _ => "Planned"
    };
    public static string NativeLane(string code) => code switch
    {
        "CPLT" or "DONE" or "VOID" => "Complete",
        "DPCH" or "ENR" => "Dispatched",
        _ => "Planned"
    };
    public static string[] AssignmentIssues(string driver, string truck) =>
        (string.IsNullOrWhiteSpace(driver) ? new[] { "Driver unassigned" } : [])
        .Concat(string.IsNullOrWhiteSpace(truck) ? new[] { "Truck unassigned" } : []).ToArray();
}

public sealed class DispatchSnapshot
{
    public List<DispatchItem> Items { get; } = [];
    public List<DispatchResource> Resources { get; } = [];
    public List<string> Warnings { get; } = [];
}
public sealed record DispatchResource(string Name, string Kind, string Terminal, string Status, long? DriverId = null);

// Read-only aggregation: dispatch changes remain in the existing revision-checked editors.
public sealed class DispatchBoardLoader(HttpClient http)
{
    async Task<T> Get<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        await ApiResponse.Check(response);
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new FormatException("Empty dispatch response.");
    }

    public async Task<DispatchSnapshot> Load(bool local, DateTime date, bool carryOver, CancellationToken ct)
    {
        var result = new DispatchSnapshot();
        if (local) await Local(result, ct);
        else await Native(result, date, carryOver, ct);
        return result;
    }

    async Task Local(DispatchSnapshot result, CancellationToken ct)
    {
        var ordersTask = Get<TmsOrder[]>("api/v1/tms/orders", ct);
        var manifestsTask = Get<TmsManifest[]>("api/v1/tms/manifests", ct);
        var terminalsTask = Get<TerminalDto[]>("api/v1/tms/terminals", ct);
        await Task.WhenAll(ordersTask, manifestsTask, terminalsTask);
        var orders = await ordersTask;
        var terminals = await terminalsTask;
        foreach (var o in orders.Where(o => o.ManifestId is null && o.Status is not ("Delivered" or "Voided")))
        {
            var issues = new List<string>();
            if (o.Status == "AwaitingAppointment") issues.Add("Awaiting appointment");
            if (!DataRequirements.HasCoordinates(o.Latitude, o.Longitude)) issues.Add("Map location missing");
            if (o.TerminalId is null && string.IsNullOrWhiteSpace(o.TerminalCode)) issues.Add("Terminal unassigned");
            result.Items.Add(new($"order:{o.SourceId}:{o.Id}", o.Id, o.Customer,
                o.TerminalCode ?? terminals.FirstOrDefault(t => t.Id == o.TerminalId)?.Code ?? "",
                o.ScheduledAt.LocalDateTime, o.AppointmentTo?.LocalDateTime, "", WorkspaceOrderStatus.Label(o.Status),
                o.OptimizationDraftId is null ? "Unassigned" : "Planned",
                o.OptimizationDraftId is {} draft ? $"/aurora/planning?draft={draft}" : "/aurora/orders",
                Orders: 1, Latitude: o.Latitude, Longitude: o.Longitude, Blockers: issues.ToArray()));
        }
        foreach (var m in await manifestsTask)
        {
            var lane = DispatchBoardRules.LocalLane(m.Status);
            var issues = lane == "Complete" ? [] : DispatchBoardRules.AssignmentIssues(m.Details.Driver, m.Vehicle);
            var terminal = terminals.FirstOrDefault(t => t.Code.Equals(m.Details.OriginTerminal, StringComparison.OrdinalIgnoreCase));
            var lateOrders = orders.Where(o => o.ManifestId == m.Id && o.Status is not ("Delivered" or "Voided") && o.AppointmentTo is not null).ToArray();
            var due = lateOrders.Length > 0 ? lateOrders.Min(o => o.AppointmentTo)!.Value.LocalDateTime : (DateTime?)null;
            result.Items.Add(new($"manifest:{m.Id}", m.Number, $"{m.Details.OriginTerminal} → {m.Details.DestinationTerminal}",
                m.Details.OriginTerminal, m.ManifestDate.ToDateTime(TimeOnly.MinValue), due, "", m.Status, lane,
                $"/aurora/manifests/{m.Id}", m.Details.Driver, m.Vehicle, m.OrderCount, terminal?.Latitude, terminal?.Longitude, issues)
            { DriverId=m.Details.DriverId, Stops = orders.Where(o => o.ManifestId == m.Id && DataRequirements.HasCoordinates(o.Latitude,o.Longitude))
                .OrderBy(o => o.StopNumber).Select((o,i) => new DispatchStop(o.Id,o.Customer,o.Latitude!.Value,o.Longitude!.Value,o.StopNumber??i+1)).ToArray() });
        }
        try
        {
            var fleet = await Get<EquipmentCatalogDto>("api/v1/routing/equipment", ct);
            result.Resources.AddRange(fleet.Units.Where(t => t.UseForRouting).Select(t => new DispatchResource(t.Id, "Truck", t.Terminal, t.Available ? "Available for planning" : "Unavailable")));
            var drivers = await Get<NovaDriver[]>("api/v1/tms/drivers", ct);
            result.Resources.AddRange(drivers.Select(d => Resource(d)));
        }
        catch (Exception ex) when (ex is HttpRequestException or FormatException) { result.Warnings.Add("Fleet directory could not be loaded. Refresh to retry."); }
    }

    async Task Native(DispatchSnapshot result, DateTime date, bool carryOver, CancellationToken ct)
    {
        // Only use local snapshots for their draft reservation, never for native operational status.
        var reservations = new Dictionary<long, Guid>();
        try
        {
            var planning = await Get<TmsOrder[]>("api/v1/tms/orders?status=ReadyToRoute", ct);
            foreach (var order in planning.Where(o => o.OptimizationDraftId is not null))
            {
                var id = order.NativeOrderId ?? (order.Id.StartsWith("FB-",StringComparison.Ordinal) && long.TryParse(order.Id.AsSpan(3),out var nativeId) ? nativeId : 0);
                if (id > 0) reservations[id] = order.OptimizationDraftId!.Value;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or FormatException)
        { result.Warnings.Add("Open plan reservations could not be loaded. Some unassigned orders may already be held in a plan."); }
        // Read native records, never stale planning snapshots. Follow pagination explicitly.
        for (var page = 0; ; page++)
        {
            var rows = await Get<NovaRecordPage>($"api/v1/aurora/firebird/records/orders?page={page}", ct);
            foreach (var o in rows.Records.Where(o => o.ManifestNumbers.Length == 0 && !DispatchBoardRules.NativeOrderClosed(o)))
                result.Items.Add(new($"order:{o.Id}", o.Number, o.Description, o.Terminal,
                    o.AppointmentFrom ?? o.AppointmentTo, o.AppointmentTo, o.TimeZone, o.Status, reservations.ContainsKey(o.Id) ? "Planned" : "Unassigned",
                    reservations.TryGetValue(o.Id,out var draft) ? $"/aurora/planning?draft={draft}" : $"/aurora/orders?edit={o.Id}", Orders: 1, Latitude: o.Latitude, Longitude: o.Longitude,
                    Blockers: DispatchBoardRules.NativeOrderIssues(o)));
            if (!rows.HasMore) break;
            if (page == 199) { result.Warnings.Add("Order scan limited to 10,000 records. Counts may be incomplete; use Orders to search older work."); break; }
        }
        var list = await Get<NovaManifestList>("api/v1/aurora/firebird/manifests?allDates=true&manifestDates=true", ct);
        // The dispatch document supplies authoritative status codes and validation issues.
        using var gate = new SemaphoreSlim(4);
        var tasks = list.Manifests.Where(m => m.StatusCode != "VOID" && (m.ManifestDate is null || m.ManifestDate.Value.Date == date.Date || carryOver && m.ManifestDate.Value.Date < date.Date && DispatchBoardRules.NativeLane(m.StatusCode) != "Complete"))
            .Select(async m =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var d = await Get<NovaDispatchDocument>($"api/v1/aurora/firebird/manifests/{m.Id}/dispatch", ct);
                    var lane = DispatchBoardRules.NativeLane(d.StatusCode);
                    var orders = lane == "Complete" ? [] : await Get<NovaManifestOrder[]>($"api/v1/aurora/firebird/manifests/{m.Id}/orders", ct);
                    var due = orders.Where(o => !o.Completed).Select(o => o.AppointmentTo).Min();
                    var driver = d.Drivers.FirstOrDefault(x => x.Id == d.DriverId)?.Name ?? m.Driver;
                    var issues = lane == "Complete" ? [] : d.Issues.Concat(DispatchBoardRules.AssignmentIssues(driver, m.Truck))
                        .Concat(m.ConsolidationHeld ? new[] { "Consolidation held" } : []).Distinct().ToArray();
                    return new DispatchItem($"manifest:{m.Id}", m.Id.ToString(), $"{m.StartTerminal} → {m.EndTerminal}", m.Terminal,
                        m.ManifestDate, due, d.TimeZone, d.Status, lane, $"/aurora/manifests?edit={m.Id}", driver, m.Truck,
                        m.OrderCount, m.DepotLatitude, m.DepotLongitude, issues)
                    { DriverId=d.DriverId, Stops = m.IsTransfer ? (DataRequirements.HasCoordinates(m.EndLatitude,m.EndLongitude)
                        ? [new DispatchStop(m.Id.ToString(),m.EndTerminal,m.EndLatitude!.Value,m.EndLongitude!.Value,1)] : [])
                        : orders.Where(o => DataRequirements.HasCoordinates(o.Latitude,o.Longitude))
                            .Select(o => new DispatchStop(o.Pro,o.Customer,o.Latitude!.Value,o.Longitude!.Value,o.Sequence)).ToArray() };
                }
                finally { gate.Release(); }
            }).ToArray();
        result.Items.AddRange(await Task.WhenAll(tasks));
        try
        {
            var drivers = await Get<DriverDirectory>("api/v1/aurora/firebird/drivers", ct);
            result.Resources.AddRange(drivers.Drivers.Select(Resource));
            for (var page = 0; ; page++)
            {
                var fleet = await Get<NovaRecordPage>($"api/v1/aurora/firebird/records/fleet?page={page}", ct);
                result.Resources.AddRange(fleet.Records.Select(t => new DispatchResource(t.Number, "Equipment", t.Terminal, t.Issues.Length > 0 ? "Needs setup" : "Active")));
                if (!fleet.HasMore) break;
                if (page == 199) { result.Warnings.Add("Equipment directory limited to 10,000 records."); break; }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or FormatException) { result.Warnings.Add("Fleet directory could not be loaded. Refresh to retry."); }
    }

    static DispatchResource Resource(NovaDriver d) => new(d.Name, "Driver", d.Terminal,
        d.TerminationDate <= DateTime.Today ? "Inactive" : d.LicenseExpiry < DateTime.Today ? "License expired" : "Active", d.Id);
}
