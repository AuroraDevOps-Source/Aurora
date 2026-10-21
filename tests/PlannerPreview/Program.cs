using System.Net;
using System.Net.Http.Json;
using Aurora.Contracts;
using PlannerPreview;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<PreviewHttp>();
builder.Services.AddScoped<Aurora.Client.Layout.SidebarState>();
builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<PreviewHttp>(), false) { BaseAddress = new Uri("http://preview.invalid/") });
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Preview>().AddInteractiveServerRenderMode();
app.Run();

public sealed class PreviewHttp : HttpMessageHandler
{
    private readonly Dictionary<string, FreightWorkflow> workflows = [];
    private readonly SemaphoreSlim workflowLock = new(1, 1);
    private static readonly Guid PreviewUser = Guid.Parse("ddd4a4a4-1693-490f-8059-82bc0761436e");
    private EquipmentCatalogDto catalog = System.Text.Json.JsonSerializer.Deserialize<EquipmentCatalogDto>(File.ReadAllText("../../src/Aurora.Client/wwwroot/samples/equipment-catalog.json"))!;
    private static readonly Guid SourceId = Guid.Parse("995d9abf-0de3-4e69-81ea-74e86a356db0");
    private readonly Dictionary<Guid, OrderDraftDto> drafts = [];
    private readonly TmsOrder[] nativeOrders = Enumerable.Range(1, 53).Select(i => new TmsOrder {
        SourceId = SourceId, Id = $"FB-{i}", TerminalCode = "KC", TerminalId = SourceId,
        PostalCode = i == 2 ? "63101" : i == 3 ? "80216" : "64101", Customer = $"Preview customer {i}", Status = WorkspaceOrderStatus.Ready
    }).ToArray();
    private readonly List<TmsOrder> orders = new[] { ("NORTH", "64116", ""), ("SOUTH", "63101", ""), ("EARLY", "64105", OrderServiceTypes.Consolidation) }.Select(x => new TmsOrder { SourceId = SourceId, Id = "TEST_" + x.Item1, Customer = "Synthetic " + x.Item1.ToLowerInvariant() + " appointment", Shipper = "Acme Distribution", OriginAddress = "100 Depot Road", OriginCity = "Kansas City", OriginState = "MO", OriginPostalCode = "64101", Address = "200 Delivery Road", BillTo = "Acme Distribution", City = x.Item2.StartsWith("63") ? "St. Louis" : "Kansas City", PostalCode = x.Item2, ServiceType = x.Item3, TerminalId = SourceId, ScheduledAt = DateTimeOffset.Now, Status = WorkspaceOrderStatus.Ready }).ToList();
    // Preview network: KC hub and STL spoke in MIDWEST, DEN hub in MOUNTAIN, and an agent near KC.
    private static readonly TerminalDto[] Nodes =
    [
        new() { Id = SourceId, Code = "KC", Name = "Kansas City", Address = "100 Depot Road", City = "Kansas City", State = "MO", PostalCode = "64101", Country = "US", Region = "MIDWEST", RoutingRole = NodeVocabulary.Hub, ServiceArea = "640-641, 660-662", Latitude = 39.124, Longitude = -94.555, OperatingHours = "Mon-Fri 05:00-20:00" },
        new() { Id = Guid.Parse("2f1d6f64-3a4b-4c1f-9d7e-1a2b3c4d5e01"), Code = "STL", Name = "St. Louis", Address = "2200 Chouteau Ave", City = "St. Louis", State = "MO", PostalCode = "63103", Country = "US", Region = "MIDWEST", RoutingRole = NodeVocabulary.Spoke, ServiceArea = "630-631", Latitude = 38.627, Longitude = -90.214 },
        new() { Id = Guid.Parse("2f1d6f64-3a4b-4c1f-9d7e-1a2b3c4d5e02"), Code = "DEN", IsConsolidation = true, Name = "Denver", Address = "4800 Race St", City = "Denver", State = "CO", PostalCode = "80216", Country = "US", Region = "MOUNTAIN", RoutingRole = NodeVocabulary.Hub, ServiceArea = "800-802", Latitude = 39.781, Longitude = -104.963 },
        new() { Id = Guid.Parse("2f1d6f64-3a4b-4c1f-9d7e-1a2b3c4d5e03"), Code = "MCI-AIR", Name = "KC Airport Cargo", Address = "1 International Square", City = "Kansas City", State = "MO", PostalCode = "64153", Country = "US", NodeType = "Airport", Functions = ["Cross-Dock"], Latitude = 39.298, Longitude = -94.714 },
    ];
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
    private NovaDispatchDocument manifestPreview = new(11465,"preview-1","Assigned",new DateTime(2026,10,14),"MHT",1,2,2,"TEST-003",true,false,null,[new(1,"Alex Morgan","MHT",null)],[new(2,"TEST-003","MHT",null)],[])
    {
        StatusCode="ASG",AllowedStatuses=["ASG","DPCH"],TimeZone="America/New_York",CanEditNotes=true,Notes="Check in at receiving before unloading.",OrderCount=3,
        PlannedRoute=PreviewManifestRoute() with { LoadUtilization=.60, Departure=new DateTimeOffset(2026,10,14,6,0,0,TimeSpan.FromHours(-4)), Return=new DateTimeOffset(2026,10,14,7,30,0,TimeSpan.FromHours(-4)) }
    };
    private NovaMasterDocument terminalPreview=new("KC","preview-1",[
        new("LM_ID","Code","text",20,true,"KC"),new("LM_NAME","Name","text",80,false,"Kansas City"),
        new("LM_ADDR1","Address","text",80,false,"100 Depot Road"),new("LM_CITY","City","text",80,false,"Kansas City"),
        new("LM_STATE","State","text",2,false,"MO"),new("LM_ZIP","ZIP","text",12,false,"64101"),
        new("LM_PHONE","Phone","text",20,false,""),new("LM_MANAGER","Manager","text",80,false,"")]);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var apiPath = request.RequestUri!.AbsolutePath;
        if(apiPath=="/api/v1/aurora/firebird/masters/terminals")
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new NovaMasterPage([new("KC",terminalPreview.Fields.Single(f=>f.Key=="LM_NAME").Value!,"Kansas City")],false))};
        if(apiPath=="/api/v1/aurora/firebird/masters/terminals/KC")
        {
            if(request.Method==HttpMethod.Put){var edit=(await request.Content!.ReadFromJsonAsync<NovaMasterEdit>(ct))!;terminalPreview=terminalPreview with {Revision=Guid.NewGuid().ToString(),Fields=terminalPreview.Fields.Select(f=>f with {Value=edit.Values[f.Key]}).ToArray()};}
            return new(HttpStatusCode.OK){Content=JsonContent.Create(terminalPreview)};
        }
        if(apiPath=="/api/v1/aurora/firebird/terminals")return new(HttpStatusCode.OK){Content=JsonContent.Create(Nodes)};
        if(apiPath=="/api/v1/aurora/firebird/terminals/KC/routing")
        {
            var edit=(await request.Content!.ReadFromJsonAsync<TerminalDto>(ct))!;
            var node=Nodes.Single(n=>n.Code=="KC");node.IsConsolidation=edit.IsConsolidation;node.ServiceArea=edit.ServiceArea;node.RoutingRole=edit.RoutingRole;node.RouteViaNodeId=null;node.OutboundRoutes=edit.OutboundRoutes;node.Revision++;
            return new(HttpStatusCode.OK){Content=JsonContent.Create(node)};
        }
        if(apiPath=="/api/v1/aurora/firebird/manifests")
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new NovaManifestList(new(2026,10,14),[new(1,"Assigned")],[new NovaManifest{Id=11465,ManifestDate=new(2026,10,14),Terminal="MHT",StartTerminal="MHT",DepotLatitude=39.124,DepotLongitude=-94.555,Driver="Alex Morgan",Truck="TEST-003",Status="Assigned",OrderCount=3}]))};
        if(apiPath=="/api/v1/aurora/firebird/manifests/11465/dispatch")
            return new(HttpStatusCode.OK){Content=JsonContent.Create(manifestPreview)};
        if(apiPath=="/api/v1/aurora/firebird/manifests/11465/notes")
        {
            var edit=(await request.Content!.ReadFromJsonAsync<NovaManifestNotesEdit>(ct))!;
            manifestPreview=manifestPreview with {Notes=edit.Notes,Revision=Guid.NewGuid().ToString()};
            return new(HttpStatusCode.OK){Content=JsonContent.Create(manifestPreview)};
        }
        if(apiPath=="/api/v1/aurora/firebird/manifests/11465/orders")
            return new(HttpStatusCode.OK){Content=JsonContent.Create(PreviewManifestRoute().Pros.Select((p,i)=>new NovaManifestOrder{Id=i+1,Pro=p.ProNumber,Sequence=i+1,Customer="Sample customer",Latitude=p.Latitude,Longitude=p.Longitude}).ToArray())};

        if (apiPath == "/api/v1/aurora/firebird/refresh")
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new FirebirdSyncResult(53, ["KC"], [], [SourceId])) };
        if (apiPath == "/api/v1/aurora/firebird/record-options")
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new NovaRecordOptions(["KC"], [new(110,"Ready")]) { TerminalNames=new(){["KC"]="Kansas City"} }) };
        if (apiPath == "/api/v1/aurora/firebird/records/orders")
        {
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri.Query);
            var pageNumber = int.TryParse(query.GetValueOrDefault("page"), out var number) ? number : 0;
            var rows = nativeOrders.Select((o,i) => new NovaRecordSummary(i+1, $"PREVIEW-{i+1}", o.Customer, "KC", "Ready")).ToArray();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new NovaRecordPage(rows.Skip(pageNumber*50).Take(50).ToArray(), rows.Length>(pageNumber+1)*50)) };
        }
        if (apiPath == "/api/v1/aurora/drafts" && request.Method == HttpMethod.Get)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(drafts.Values.Where(d=>!d.Cancelled&&!d.Finished).Select(d=>new OpenOrderDraftDto(d.Id,d.FileName,RoutingInput.Items(RoutingInput.Parse(d.RequestJson)["orders"]?["deliveries"]).Count())).ToArray()) };
        if (apiPath == "/api/v1/admin/reset/")
            return new(HttpStatusCode.OK) { Content = request.Method == HttpMethod.Get
                ? JsonContent.Create(new WorkspaceResetPreview("synthetic", 3, 2, 20))
                : JsonContent.Create(new WorkspaceResetResult(3, 2, 20)) };
        if (apiPath.Split('/', StringSplitOptions.RemoveEmptyEntries) is ["api", "v1", "tms", "orders", var workflowSource, var orderId, "workflow", .. var rest])
        {
            if (!Guid.TryParse(workflowSource, out var sourceId) || !orders.Any(o => o.SourceId == sourceId && o.Id == Uri.UnescapeDataString(orderId)))
                return new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new ApiErrorDto("Preview order not found.")) };
            await workflowLock.WaitAsync(ct);
            try
            {
                var current = workflows.GetValueOrDefault(orderId) ?? new FreightWorkflow();
                if (request.Method == HttpMethod.Put && rest.Length == 0)
                {
                    var input = (await request.Content!.ReadFromJsonAsync<FreightWorkflow>(ct))!;
                    if (input.Revision != current.Revision) throw new FreightConflictException();
                    input.ValidateReplacement(current); input.Revision++; workflows[orderId] = current = input;
                }
                else if (request.Method == HttpMethod.Post && rest is ["events"])
                {
                    var command = (await request.Content!.ReadFromJsonAsync<FreightExecution>(ct))!;
                    if (current.Execute(command, PreviewUser, DateTimeOffset.UtcNow)) current.Revision++;
                    workflows[orderId] = current;
                }
                else if (request.Method != HttpMethod.Get || rest.Length != 0)
                    return new(HttpStatusCode.MethodNotAllowed);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(current) };
            }
            catch (FreightConflictException e) { return new(HttpStatusCode.Conflict) { Content = JsonContent.Create(new ApiErrorDto(e.Message)) }; }
            catch (FormatException e) { return new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new ApiErrorDto(e.Message)) }; }
            finally { workflowLock.Release(); }
        }
        if ((apiPath.StartsWith("/api/v1/tms/") && request.Method != HttpMethod.Get) || apiPath.EndsWith("/finish"))
            return new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new ApiErrorDto("This UI preview does not save orders or manifests. Use the full application with its configured database.")) };
        if (apiPath == "/api/v1/me/tenant") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new WorkspaceTenantDto(SourceId, "Synthetic preview company")) };
        if (apiPath == "/api/v1/tms/terminals" && request.Method == HttpMethod.Get)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Nodes) };
        if (apiPath == "/api/v1/tms/customers" && request.Method == HttpMethod.Get)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new CustomerDto { Id=SourceId, Code="ACME", Name="Acme Distribution", Address="123 Customer Avenue", City="Kansas City", State="MO", PostalCode="64101", Country="US" } }) };
        if (apiPath == "/api/v1/tms/orders" && request.Method == HttpMethod.Get)
        {
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri.Query);
            var status = query.GetValueOrDefault("status").ToString(); var search = query.GetValueOrDefault("search").ToString();
            DateTimeOffset? from = DateTimeOffset.TryParse(query.GetValueOrDefault("from"), out var begin) ? begin : null;
            DateTimeOffset? to = DateTimeOffset.TryParse(query.GetValueOrDefault("to"), out var end) ? end : null;
            foreach (var order in orders.Concat(nativeOrders)) order.OptimizationDraftId = drafts.Values.FirstOrDefault(d => !d.Finished && !d.Cancelled && RoutingInput.Items(RoutingInput.Parse(d.RequestJson)["orders"]?["deliveries"]).Any(x => RoutingInput.Text(x["id"]) == order.Id))?.Id;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(orders.Concat(nativeOrders).Where(o => (status == "" || o.Status == status) && string.Join(" ", o.Id, o.Customer, o.City).Contains(search, StringComparison.OrdinalIgnoreCase) && (from is null || o.ScheduledAt >= from) && (to is null || o.ScheduledAt <= to)).ToArray()) };
        }
        if (apiPath.StartsWith("/api/v1/aurora/manifests/") && request.Method == HttpMethod.Get)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new SavedManifestDto(SourceId, Guid.NewGuid(), "ONT-247", DateTimeOffset.Now.AddHours(-2), PreviewManifestRoute())) };
        if (apiPath == "/api/v1/tms/manifests") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new TmsManifest { Id=SourceId, Number="ONT-20260929-A", Vehicle="ONT-247", Details=new(){Driver="Alex Morgan",Carrier="Golden State Freight"} } }) };
        if (apiPath is "/api/v1/aurora/drafts" or "/api/v1/aurora/plans" && request.Method == HttpMethod.Post)
        {
            var original = (await request.Content!.ReadFromJsonAsync<CreateOrderDraftDto>(ct))!;
            var requests = PlanningBatches.GroupPlans(original, orders.Concat(nativeOrders).ToArray(), Nodes[0], Nodes);
            var created = new List<OrderDraftDto>();
            foreach (var input in requests)
            {
            var root = RoutingInput.Parse(await File.ReadAllTextAsync("../../src/Aurora.Client/wwwroot/samples/appointment-test.json", ct));
            if(input.OrderIds.Any(id=>id.StartsWith("FB-")))
            {
                var template=RoutingInput.Items(root["orders"]?["deliveries"]).First();
                root["orders"]!["deliveries"]=new System.Text.Json.Nodes.JsonArray(input.OrderIds.Select(id=>{var copy=template.DeepClone();copy["id"]=id;return copy;}).ToArray());
            }
            root["orders"]!["deliveries"] = new System.Text.Json.Nodes.JsonArray(RoutingInput.Items(root["orders"]?["deliveries"]).Where(o => input.OrderIds.Contains(RoutingInput.Text(o["id"]))).Select(o => o.DeepClone()).ToArray());
            var assignments = orders.Concat(nativeOrders).Where(o => input.OrderIds.Contains(o.Id)).Select(o => PlanningBatches.Classify(o, Nodes[0], Nodes)).ToArray();
            var batch = PlanningBatch.For(input.Batch, assignments);
            var destinations = assignments.Where(a => a.DestinationNodeId is not null).ToDictionary(a => a.OrderId, a => Nodes.Single(n => n.Id == a.DestinationNodeId));
            var json = PlanningBatches.ApplyDestinations(root.ToJsonString(), destinations);
            var receiving = Nodes.SingleOrDefault(n => n.Id == input.ToTerminalId);
            if (receiving is not null) json = LineHaulPlanning.SetFleetDestination(LineHaulPlanning.Prepare(json, Nodes[0], receiving));
            if (input.ShipDate is { } shipDate) json = PlannerEdits.FleetDay(json, shipDate, input.DepartAt ?? "06:00", input.ReturnBy ?? "18:00");
            var draft = new OrderDraftDto(Guid.NewGuid(), SourceId, $"{PlanningBatch.Label(batch)} · KC" + (receiving is null ? "" : $" → {receiving.Code}") + (input.ShipDate is { } day ? $" · {day:MMM d}" : ""), json, false, batch);
            drafts[draft.Id] = draft;
            created.Add(draft);
            }
            return new(HttpStatusCode.OK) { Content = apiPath.EndsWith("/plans") ? JsonContent.Create(created.ToArray()) : JsonContent.Create(created.Single()) };
        }
        if (apiPath.StartsWith("/api/v1/aurora/drafts/") && apiPath.EndsWith("/cancel") && request.Method == HttpMethod.Post)
        {
            var draftId = Guid.Parse(apiPath.Split('/')[5]);
            if (drafts.TryGetValue(draftId, out var open)) drafts[draftId] = open with { Cancelled = true };
            return new(HttpStatusCode.OK);
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
            var finished = session.Stopped || elapsed >= 2;
            var selectedIds = RoutingInput.Items(RoutingInput.Parse(session.Input.RequestJson)["orders"]?["deliveries"]).Select(o => RoutingInput.Text(o["id"])).OfType<string>().ToHashSet();
            var routes = PreviewRoutes().Where(r => selectedIds.Contains(r.Pros[0].ProNumber)).ToArray();
            DroppedOrderDto[] dropped = selectedIds.Contains("TEST_EARLY") ? [new("TEST_EARLY", 39.13, -94.55, "Appointment is before the truck shift (synthetic example)", "Synthetic early appointment", "", "", "", "", 1, 100, 10, "", 900)] : [];
            if(selectedIds.Any(x=>x.StartsWith("FB-")))
            {
                var scheduled=selectedIds.Take(Math.Max(1,selectedIds.Count-1)).ToArray();
                var template=PreviewManifestRoute();
                var pros=scheduled.Select((orderId,i)=>template.Pros[i%template.Pros.Count] with { ProNumber=orderId, StopNumber=i+1, ShipToName=$"Preview delivery {orderId}" }).ToArray();
                routes=[template with { Vehicle="TEST_TRUCK_1", LoadUtilization=0.6, TimeUtilization=0.5, Orders=pros.Length, Stops=pros.Length, ManifestStops=pros.Length, Pros=pros,
                    RouteStops=[template.RouteStops[0], ..pros.Select(p=>new RouteStopDto(p.ProNumber,p.Latitude??39.1,p.Longitude??-94.5,null,null,false,[p.ProNumber])),template.RouteStops[^1]] }];
                dropped=selectedIds.Except(scheduled).Select(orderId=>new DroppedOrderDto(orderId,39.13,-94.55,"Outside truck hours (synthetic example)","Preview delivery", "", "", "", "",1,100,10,"",900)).ToArray();
            }
            var inputRoot = RoutingInput.Parse(session.Input.RequestJson);
            // Simulate accepted manual moves so the UI preview can exercise recalculation and drag/drop.
            if (inputRoot["reporting"]?["orderDecisions"] is System.Text.Json.Nodes.JsonObject decisions
                && decisions.Any(d => RoutingInput.Text(d.Value)?.StartsWith("truck:", StringComparison.Ordinal) == true))
            {
                var template = PreviewManifestRoute();
                routes = routes.SelectMany(r => r.Pros.Select(p => (Pro: p, Truck: PlanOrderDecisions.Get(inputRoot, p.ProNumber) is var decision && decision.StartsWith("truck:", StringComparison.Ordinal) ? decision[6..] : r.Vehicle)))
                    .GroupBy(x => x.Truck).Select(group =>
                    {
                        var pros = group.Select((x, i) => x.Pro with { StopNumber = i + 1 }).ToArray();
                        return template with { Vehicle = group.Key, LoadUtilization = pros.Length * .1, Orders = pros.Length, Stops = pros.Length,
                            ManifestStops = pros.Length, Pros = pros, Weight = pros.Sum(p => p.Weight ?? 0), Cubes = pros.Sum(p => p.Cubes ?? 0), Units = pros.Sum(p => p.Units ?? 0),
                            RouteStops = [template.RouteStops[0], ..pros.Select(p => new RouteStopDto(p.ProNumber, p.Latitude ?? 39.1, p.Longitude ?? -94.5, null, null, false, [p.ProNumber])), template.RouteStops[^1]] };
                    }).ToArray();
            }
            var endId = RoutingInput.Text(inputRoot["reporting"]?["lineHaulEndLocation"]);
            if (endId is not null)
            {
                var destination = RoutingInput.Items(inputRoot["locations"]).Single(l=>RoutingInput.Text(l["id"])==endId);
                var lat=destination["latitude"]!.GetValue<double>(); var lng=destination["longitude"]!.GetValue<double>();
                routes=routes.Select(r=>r with { Stops=1,ManifestStops=1,
                    Pros=r.Pros.Select(p=>p with {StopNumber=1,Latitude=lat,Longitude=lng,ShipToName=RoutingInput.Text(inputRoot["reporting"]?["lineHaulTo"])!}).ToArray(),
                    RouteStops=[r.RouteStops[0],new RouteStopDto(endId,lat,lng,null,null,false,r.Pros.Select(p=>p.ProNumber).ToArray())] }).ToArray();
            }
            var sample = new PlanningSampleDto(DateTimeOffset.UtcNow, routes.Sum(r=>r.Orders), dropped.Length, routes.Length, routes.Length * 110);
            var summary = new RunSummaryDto("SUCCEEDED", routes.Length, 2, routes.Sum(r=>r.Orders), dropped.Length, routes.Length * 15, routes.Length * 1800, routes.Length * 110, 0, selectedIds.Count, 4, 110, 15, 1, "mixed");
            var raw = "{\"status\":\"SUCCEEDED\",\"routes\":[]}";
            var result = finished ? new OptimizationResultDto(session.Input.FileName, id.ToString(), DateTimeOffset.UtcNow, "DEMO", "SUCCEEDED", 200, "200 OK", elapsed, false, summary, routes, dropped, [], [], [], [], raw, null, null) : null;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new PlanningSessionDto(id, finished ? "SUCCEEDED" : "RUNNING", session.Input.FileName, session.Input.RequestJson, elapsed, [sample], result) { Candidates=finished ? [new("preview-best",DateTimeOffset.UtcNow,summary)] : [], AcceptedCandidateId=path.Contains("/accept/") ? "preview-best" : null }) };
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

