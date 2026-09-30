using System.Net;
using System.Net.Http.Json;
using Aurora.Contracts;
using PlannerPreview;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<PreviewHttp>();
builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<PreviewHttp>(), false) { BaseAddress = new Uri("http://preview.invalid/") });
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Preview>().AddInteractiveServerRenderMode();
app.Run();

public sealed class PreviewHttp : HttpMessageHandler
{
    private EquipmentCatalogDto catalog = System.Text.Json.JsonSerializer.Deserialize<EquipmentCatalogDto>(File.ReadAllText("../../src/Aurora.Client/wwwroot/samples/equipment-catalog.json"))!;
    private static readonly Guid SourceId = Guid.Parse("995d9abf-0de3-4e69-81ea-74e86a356db0");
    private readonly Dictionary<Guid, OrderDraftDto> drafts = [];
    private readonly List<TmsOrder> orders = new[] { "NORTH", "SOUTH", "EARLY" }.Select(x => new TmsOrder { SourceId = SourceId, Id = "TEST_" + x, Customer = "Synthetic " + x.ToLowerInvariant() + " appointment", City = "Kansas City", ScheduledAt = DateTimeOffset.Now, Status = WorkspaceOrderStatus.Ready }).ToList();
    private readonly Dictionary<Guid, (StartPlanningDto Input, DateTimeOffset Start, bool Stopped)> sessions = [];
    private static RouteSummaryDto[] PreviewRoutes() => new[] { ("TEST_TRUCK_1", "TEST_NORTH", 39.15, -94.55), ("TEST_TRUCK_2", "TEST_SOUTH", 39.10, -94.56) }.Select(x =>
        new RouteSummaryDto(x.Item1, 1, 1, 15, 1800, 110, 50, 20, "DEMO", 32400, 2700, 1, 1, 100, 10,
            [new ManifestProDto(x.Item2, 1, x.Item2, "Synthetic destination", "Kansas City", "MO", "", 1, 100, 10, "", 900, x.Item3, x.Item4)],
            [new RouteStopDto("DEPOT", 39.124, -94.555, null, null, true, []), new RouteStopDto(x.Item2, x.Item3, x.Item4, null, null, false, [x.Item2]), new RouteStopDto("DEPOT", 39.124, -94.555, null, null, true, [])])).ToArray();
    // The synthetic manifest as route optimization would have saved it: one truck through all three orders.
    private static RouteSummaryDto PreviewManifestRoute()
    {
        var stops = new[] { ("TEST_NORTH", 39.15, -94.55), ("TEST_EARLY", 39.13, -94.54), ("TEST_SOUTH", 39.10, -94.56) };
        RouteStopDto depot = new("DEPOT", 39.124, -94.555, null, null, true, []);
        return new RouteSummaryDto("ONT-247", 3, 3, 38, 3600, 180, 60, 40, "DEMO", 32400, 5400, 3, 3, 300, 30,
            stops.Select((s, i) => new ManifestProDto(s.Item1, i + 1, "Synthetic " + s.Item1, "Synthetic destination", "Kansas City", "MO", "", 1, 100, 10, "", 900, s.Item2, s.Item3)).ToArray(),
            [depot, .. stops.Select(s => new RouteStopDto(s.Item1, s.Item2, s.Item3, null, null, false, [s.Item1])), depot]);
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var apiPath = request.RequestUri!.AbsolutePath;
        if ((apiPath.StartsWith("/api/v1/tms/") && request.Method != HttpMethod.Get) || apiPath.EndsWith("/finish"))
            return new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new ApiErrorDto("This UI preview does not save orders or manifests. Use the full application with its configured database.")) };
        if (apiPath == "/api/v1/me/tenant") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new WorkspaceTenantDto(SourceId, "Synthetic preview company")) };
        if (apiPath == "/api/v1/tms/terminals" && request.Method == HttpMethod.Get)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new TerminalDto { Id=SourceId, Code="KC", Name="Kansas City", Address="100 Depot Road", City="Kansas City", State="MO", PostalCode="64101", Country="US" } }) };
        if (apiPath == "/api/v1/tms/customers" && request.Method == HttpMethod.Get)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new CustomerDto { Id=SourceId, Code="ACME", Name="Acme Distribution", Address="123 Customer Avenue", City="Kansas City", State="MO", PostalCode="64101", Country="US" } }) };
        if (apiPath == "/api/v1/tms/orders" && request.Method == HttpMethod.Get)
        {
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri.Query);
            var status = query.GetValueOrDefault("status").ToString(); var search = query.GetValueOrDefault("search").ToString();
            DateTimeOffset? from = DateTimeOffset.TryParse(query.GetValueOrDefault("from"), out var begin) ? begin : null;
            DateTimeOffset? to = DateTimeOffset.TryParse(query.GetValueOrDefault("to"), out var end) ? end : null;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(orders.Where(o => (status == "" || o.Status == status) && string.Join(" ", o.Id, o.Customer, o.City).Contains(search, StringComparison.OrdinalIgnoreCase) && (from is null || o.ScheduledAt >= from) && (to is null || o.ScheduledAt <= to)).ToArray()) };
        }
        if (apiPath.StartsWith("/api/v1/aurora/manifests/") && request.Method == HttpMethod.Get)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new SavedManifestDto(SourceId, Guid.NewGuid(), "ONT-247", DateTimeOffset.Now.AddHours(-2), PreviewManifestRoute())) };
        if (apiPath == "/api/v1/tms/manifests") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new TmsManifest { Id=SourceId, Number="ONT-20260929-A", Vehicle="ONT-247", Details=new(){Driver="Alex Morgan",Carrier="Golden State Freight"} } }) };
        if (apiPath == "/api/v1/aurora/drafts" && request.Method == HttpMethod.Post)
        {
            var input = (await request.Content!.ReadFromJsonAsync<CreateOrderDraftDto>(ct))!;
            var root = RoutingInput.Parse(await File.ReadAllTextAsync("../../src/Aurora.Client/wwwroot/samples/appointment-test.json", ct));
            root["orders"]!["deliveries"] = new System.Text.Json.Nodes.JsonArray(RoutingInput.Items(root["orders"]?["deliveries"]).Where(o => input.OrderIds.Contains(RoutingInput.Text(o["id"]))).Select(o => o.DeepClone()).ToArray());
            var json = root.ToJsonString();
            if (input.ShipDate is { } shipDate) json = PlannerEdits.FleetDay(json, shipDate, input.DepartAt ?? "06:00", input.ReturnBy ?? "18:00");
            var draft = new OrderDraftDto(Guid.NewGuid(), SourceId, "Selected orders", json, false);
            drafts[draft.Id] = draft;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(draft) };
        }
        if (apiPath.StartsWith("/api/v1/aurora/drafts/") && request.Method == HttpMethod.Get)
        {
            var draftId = Guid.Parse(apiPath.Split('/')[5]);
            return drafts.TryGetValue(draftId, out var draft) ? new(HttpStatusCode.OK) { Content = JsonContent.Create(draft) } : new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new ApiErrorDto("Preview draft not found. Start again from Orders.")) };
        }
        if (request.RequestUri!.AbsolutePath.EndsWith("appointment-test.json"))
            return new(HttpStatusCode.OK) { Content = new StringContent(await File.ReadAllTextAsync("../../src/Aurora.Client/wwwroot/samples/appointment-test.json", ct)) };
        if (request.RequestUri.AbsolutePath.Contains("/sessions"))
        {
            var path = request.RequestUri.AbsolutePath;
            if (path.EndsWith("/sessions"))
            {
                var input = (await request.Content!.ReadFromJsonAsync<StartPlanningDto>(ct))!;
                sessions[input.Id] = (input, DateTimeOffset.UtcNow, false);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new PlanningSessionDto(input.Id, "QUEUING", input.FileName, input.RequestJson, 0, [], null)) };
            }
            var parts = path.Split('/');
            var id = Guid.Parse(parts[5]);
            if (!sessions.TryGetValue(id, out var session)) return new(HttpStatusCode.NotFound);
            if (path.EndsWith("/stop")) { session.Stopped = true; sessions[id] = session; }
            var elapsed = (DateTimeOffset.UtcNow - session.Start).TotalSeconds;
            var finished = session.Stopped || elapsed >= 45;
            var selectedIds = RoutingInput.Items(RoutingInput.Parse(session.Input.RequestJson)["orders"]?["deliveries"]).Select(o => RoutingInput.Text(o["id"])).ToHashSet();
            var routes = PreviewRoutes().Where(r => selectedIds.Contains(r.Pros[0].ProNumber)).ToArray();
            DroppedOrderDto[] dropped = selectedIds.Contains("TEST_EARLY") ? [new("TEST_EARLY", 39.13, -94.55, "Appointment is before the truck shift (synthetic example)", "Synthetic early appointment", "", "", "", "", 1, 100, 10, "", 900)] : [];
            var sample = new PlanningSampleDto(DateTimeOffset.UtcNow, routes.Length, dropped.Length, routes.Length, routes.Length * 110);
            var summary = new RunSummaryDto("SUCCEEDED", routes.Length, 2, routes.Length, dropped.Length, routes.Length * 15, routes.Length * 1800, routes.Length * 110, 0, selectedIds.Count, 4, 110, 15, 1, "mixed");
            var raw = "{\"status\":\"SUCCEEDED\",\"routes\":[]}";
            var result = finished ? new OptimizationResultDto(session.Input.FileName, id.ToString(), DateTimeOffset.UtcNow, "DEMO", "SUCCEEDED", 200, "200 OK", elapsed, false, summary, routes, dropped, [], [], [], [], raw, null, null) : null;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new PlanningSessionDto(id, finished ? "SUCCEEDED" : "RUNNING", session.Input.FileName, session.Input.RequestJson, elapsed, [sample], result)) };
        }
        if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.EndsWith("types"))
        {
            var item = (await request.Content!.ReadFromJsonAsync<EquipmentTypeDto>(ct))!;
            catalog = catalog with { Types = catalog.Types.Where(x => x.Code != item.Code).Append(item).ToArray() };
        }
        if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.EndsWith("units"))
        {
            var item = (await request.Content!.ReadFromJsonAsync<EquipmentUnitDto>(ct))!;
            catalog = catalog with { Units = catalog.Units.Where(x => x.Id != item.Id).Append(item).ToArray() };
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(catalog) };
    }
}
