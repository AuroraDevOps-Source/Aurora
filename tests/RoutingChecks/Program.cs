using Aurora.Contracts;
using Aurora.Modules.Routing.Optimization;
using System.Text.Json.Nodes;

var count = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); count++; }
void Reject(Action action, string name) { try { action(); } catch (FormatException) { Check(true, name); return; } throw new Exception("FAIL: " + name); }
var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample.json"));
var original = RoutingInput.Parse(json);
var preparedText = RoutingInput.Prepare(json);
var prepared = RoutingInput.Parse(preparedText);
Check(original["locations"]!.AsArray().Count == 4, "original JSON is untouched");
Check(prepared["locations"]!.AsArray().Count == 7, "three confirmed appointments get isolated locations");
var first = prepared["orders"]!["deliveries"]![0]!;
var firstLocation = RoutingInput.Items(prepared["locations"]).Single(x => RoutingInput.Text(x["id"]) == RoutingInput.Text(first["delivery"]!["locationId"]));
Check(first["delivery"]!["timeSlotIds"]![0]!.GetValue<string>() == "APPT_0", "task points at appointment slot");
Check(firstLocation["stopProperties"]!["timeSlots"]![0]!["earliestStart"]!.GetValue<string>().Contains("09:00:00"), "appointment start reaches canonical PTV input");
Check(JsonNode.DeepEquals(prepared, RoutingInput.Parse(RoutingInput.Prepare(preparedText))), "preparation is idempotent");
Check(RoutingInput.Parse(ManifestReportExtractor.RemoveReportingSidecar(preparedText))["reporting"] is null, "display metadata is removed before PTV");
Check(RoutingInput.Describe(prepared, "TEST_NORTH") is { Enforced: true, Confirmed: true }, "confirmed status is reported");

var pending = RoutingInput.Parse(json);
pending["reporting"]!["orders"]!["TEST_NORTH"]!["appointment"]!["confirmed"] = false;
var pendingOutput = RoutingInput.Parse(RoutingInput.Prepare(pending.ToJsonString()));
Check(RoutingInput.Text(pendingOutput["orders"]!["deliveries"]![0]!["delivery"]!["locationId"]) == "NORTH", "unconfirmed appointment does not constrain routing");
Check(RoutingInput.Describe(pendingOutput, "TEST_NORTH") is { Enforced: false, Confirmed: false }, "unconfirmed appointment remains visible");

var unrestricted = RoutingInput.Parse(json);
unrestricted.Remove("reporting");
Check(JsonNode.DeepEquals(unrestricted, RoutingInput.Parse(RoutingInput.Prepare(unrestricted.ToJsonString()))), "existing files without appointments remain unchanged");

var opening = RoutingInput.Parse(json);
opening["locations"]![1]!["stopProperties"] = JsonNode.Parse("""{"timeSlots":[{"id":"OPEN","earliestStart":"2030-01-01T08:30:00-06:00","latestStart":"2030-01-01T10:00:00-06:00","latestEnd":"2030-01-01T10:30:00-06:00","preparationDuration":60}]}""");
var intersect = RoutingInput.Parse(RoutingInput.Prepare(opening.ToJsonString()));
var intersection = RoutingInput.Items(intersect["locations"]).Single(x => RoutingInput.Text(x["id"]) == RoutingInput.Text(intersect["orders"]!["deliveries"]![0]!["delivery"]!["locationId"]))["stopProperties"]!["timeSlots"]![0]!;
Check(intersection["latestEnd"]!.GetValue<string>().Contains("10:30"), "existing finish deadline is preserved");
Check(intersection["preparationDuration"]!.GetValue<int>() == 60, "existing slot properties are preserved");
opening["locations"]![1]!["stopProperties"]!["timeSlots"]![0]!["earliestStart"] = "2030-01-01T10:00:00-06:00";
Reject(() => RoutingInput.Prepare(opening.ToJsonString()), "conflicting opening hours fail before spending a PTV call");
Reject(() => RoutingInput.Prepare(json.Replace("2030-01-01T09:00:00-06:00", "2030-01-01T09:00:00")), "ambiguous timezone is rejected");
var backwards = RoutingInput.Parse(json);
backwards["reporting"]!["orders"]!["TEST_NORTH"]!["appointment"]!["blockEnd"] = "2030-01-01T08:59:00-06:00";
Reject(() => RoutingInput.Prepare(backwards.ToJsonString()), "reversed appointment is rejected");

const string csv = "PRO,AppointmentStart,AppointmentEnd,AppointmentConfirmed\r\nTEST_NORTH,2030-01-01T10:00:00-06:00,2030-01-01T11:00:00-06:00,true\r\n";
var imported = RoutingInput.ImportCsv(json, csv);
Check(imported.Imported == 1, "CSV merges by exact PRO");
Check(RoutingInput.Describe(RoutingInput.Parse(imported.Json), "TEST_NORTH")!.Windows[0].EarliestStart!.Value.Hour == 10, "CSV updates appointment bounds");
Check(RoutingInput.Text(RoutingInput.Parse(imported.Json)["reporting"]!["orders"]!["TEST_NORTH"]!["shipToName"]) == "Synthetic north appointment", "CSV preserves existing shipment metadata");
Check(RoutingInput.ImportCsv(json, RoutingInput.AppointmentTemplate(json)).Imported == 3, "template round-trips all appointment rows");
Reject(() => RoutingInput.ImportCsv(json, csv + csv.Split('\n')[1]), "duplicate CSV PRO is rejected atomically");
Reject(() => RoutingInput.ImportCsv(json, csv.Replace("TEST_NORTH", "UNKNOWN")), "unknown CSV PRO is rejected");
Reject(() => RoutingInput.ImportCsv(json, csv.Replace("true", "maybe")), "invalid confirmation is rejected");
Reject(() => RoutingInput.ImportCsv(json, csv.Replace("TEST_NORTH", "\"TEST_NORTH")), "broken CSV quoting is rejected");
Check(RoutingInput.Csv("=HYPERLINK(\"bad\")").StartsWith("\"'="), "spreadsheet formula labels are escaped");
Check(RoutingInput.Csv("-94.56") == "\"-94.56\"" && RoutingInput.Csv("-1+2").StartsWith("\"'"), "negative coordinates stay numeric while expressions remain escaped");

var arraySidecar = RoutingInput.Parse(json);
arraySidecar["reporting"]!["orders"] = new JsonArray(new JsonObject { ["proNumber"] = "TEST_NORTH", ["AppointmentDetail"] = new JsonObject { ["BlockBegin"] = "2030-01-01T09:00:00-06:00", ["BlockEnd"] = "2030-01-01T09:30:00-06:00", ["Confirmed"] = true } });
Check(RoutingInput.Describe(RoutingInput.Parse(RoutingInput.Prepare(arraySidecar.ToJsonString())), "TEST_NORTH")!.Enforced, "FreightOps PascalCase fields and array sidecars are supported");

var locationId = RoutingInput.Text(first["delivery"]!["locationId"]);
var response = new JsonObject
{
    ["status"] = "SUCCEEDED", ["metrics"] = new JsonObject { ["numberOfRoutes"] = 1, ["numberOfScheduledOrders"] = 1, ["numberOfUnscheduledOrders"] = 1, ["totalDistance"] = 10000, ["totalCost"] = 135.5 },
    ["routes"] = new JsonArray(new JsonObject
    {
        ["vehicleId"] = "TEST_TRUCK_1", ["start"] = new JsonObject { ["locationId"] = "DEPOT", ["departure"] = "2030-01-01T08:30:00-06:00" },
        ["end"] = new JsonObject { ["locationId"] = "DEPOT", ["arrival"] = "2030-01-01T10:00:00-06:00" },
        ["metrics"] = new JsonObject { ["cost"] = 135.5, ["distance"] = 10000, ["numberOfOrders"] = 1, ["numberOfStops"] = 1, ["durations"] = new JsonObject { ["driving"] = 1800 } },
        ["stops"] = new JsonArray(new JsonObject { ["locationId"] = locationId, ["arrival"] = "2030-01-01T08:55:00-06:00", ["departure"] = "2030-01-01T09:15:00-06:00", ["appointments"] = new JsonArray(new JsonObject { ["tasks"] = new JsonArray(new JsonObject { ["orderId"] = "TEST_NORTH" }) }) })
    }),
    ["unscheduledOrders"] = new JsonArray(new JsonObject { ["id"] = "TEST_EARLY" })
};
var report = ManifestReportExtractor.ExtractPtv(response.ToJsonString(), preparedText);
Check(report.Routes["TEST_TRUCK_1"].Departure!.Value.Hour == 8 && report.Routes["TEST_TRUCK_1"].Return!.Value.Hour == 10, "route departure and return are exposed");
Check(report.Routes["TEST_TRUCK_1"].Pros[0] is { Appointment.Enforced: true, Arrival: not null, Departure: not null }, "manifest joins appointments and stop schedule");
Check(report.Dropped["TEST_EARLY"].Appointment!.Enforced, "unscheduled orders retain appointment context");
Check(RunSummary.Parse(response.ToJsonString(), preparedText, 1, "test").Routes[0].Cost == 135.5, "per-manifest cost is provider cost, not a stop allocation");
var utcResponse = response.ToJsonString().Replace("2030-01-01T08:30:00-06:00", "2030-01-01T14:30:00Z")
    .Replace("2030-01-01T10:00:00-06:00", "2030-01-01T16:00:00Z")
    .Replace("2030-01-01T08:55:00-06:00", "2030-01-01T14:55:00Z")
    .Replace("2030-01-01T09:15:00-06:00", "2030-01-01T15:15:00Z");
var localReport = ManifestReportExtractor.ExtractPtv(utcResponse, preparedText).Routes["TEST_TRUCK_1"];
Check(localReport.Departure!.Value.Hour == 8 && localReport.Departure.Value.Offset == TimeSpan.FromHours(-6) && localReport.Return!.Value.Hour == 10, "UTC route times use the vehicle's explicit offset");
Check(localReport.Pros[0].Arrival!.Value.Hour == 8 && localReport.Pros[0].Departure!.Value.Hour == 9, "stop schedules use the vehicle offset without changing instants");
Check(RouteDetailExtractor.ExtractPtv(utcResponse, preparedText).Routes[0].Stops[0].Arrival!.Value.Hour == 8, "map schedule matches the manifest timezone");
var locationReporting = RoutingInput.Parse(json);
locationReporting["reporting"]!["locations"] = JsonNode.Parse("""{"NORTH":{"address1":"Synthetic test street","city":"Test City"}}""");
var enrichedReport = ManifestReportExtractor.ExtractPtv(response.ToJsonString(), RoutingInput.Prepare(locationReporting.ToJsonString()));
Check(enrichedReport.Routes["TEST_TRUCK_1"].Pros[0].Address1 == "Synthetic test street", "location reporting fields follow isolated appointment locations");
locationReporting["reporting"]!["locations"] = JsonNode.Parse("""[{"Id":"NORTH","address1":"Synthetic array street"}]""");
Check(ManifestReportExtractor.ExtractPtv(response.ToJsonString(), RoutingInput.Prepare(locationReporting.ToJsonString())).Routes["TEST_TRUCK_1"].Pros[0].Address1 == "Synthetic array street", "array location metadata follows isolated locations");
var assigned = RoutingInput.Parse(json);
assigned["routes"] = JsonNode.Parse("""[{"vehicleId":"TEST_TRUCK_1","stops":[]}]""");
Reject(() => RoutingInput.Prepare(assigned.ToJsonString()), "preassigned route constraints cannot be silently changed by appointment import");
Console.WriteLine($"{count} routing checks passed.");

var draft = Aurora.Client.RoutingDraft.Read(json);
Check(draft.Orders == 3 && draft.Trucks == 2 && draft.Windows == 3, "workspace draft counts reflect the imported file");
Check(draft.Points.Count(p => !p.IsDepot) == 3 && draft.Points.Count(p => p.IsDepot) == 1, "preview contains actual deliveries and one deduplicated depot");
Check(draft.Deliveries[0].Name == "Synthetic north appointment" && draft.Deliveries[0].Appointment!.Enforced, "input preview includes shipment names and appointments");
Check(Aurora.Client.RoutingDraft.Read(pending.ToJsonString()).InformationalAppointments == 1, "draft distinguishes informational appointments from constraints");
var missingCoordinate = RoutingInput.Parse(json);
missingCoordinate["locations"]![1]!.AsObject().Remove("latitude");
var missingPreview = Aurora.Client.RoutingDraft.Read(missingCoordinate.ToJsonString());
Check(missingPreview.Orders == 3 && missingPreview.Deliveries.Count(d => !d.HasCoordinates) == 1, "orders with missing coordinates stay in the plan but are flagged in preview");
missingCoordinate["locations"]![1]!["latitude"] = 999;
Check(Aurora.Client.RoutingDraft.Read(missingCoordinate.ToJsonString()).Points.Count(p => !p.IsDepot) == 2, "invalid coordinates are not drawn on the preview map");
try { _ = Aurora.Client.RoutingDraft.Read("{}"); throw new Exception("Expected malformed input to fail"); } catch (FormatException) { }
Check(draft.Orders == 3, "invalid replacement cannot mutate the existing immutable draft");
Console.WriteLine($"{count} total routing and workspace checks passed.");

var equipment = new EquipmentTypeDto { Code = "BOX26", Description = "26 foot box truck", Weight = 12000, Cubes = 1683, Categories = "LIFTGATE", MaximumStops = 12, PerHour = 65 };
var equipped = EquipmentPlanning.Vehicle(equipment, "UNIT1", "DEPOT", "2030-01-01T22:00:00-06:00", "2030-01-02T08:00:00-06:00");
Check(equipped["end"]!["latestEndTime"]!.GetValue<string>().Contains("2030-01-02"), "equipment supports overnight shifts without changing the date or offset");
Check(equipped["constraints"]!["maximumLoads"]![0]!["value"]!.GetValue<double>() == 12000 && equipped["constraints"]!["maximumLoads"]![1]!["value"]!.GetValue<double>() == 1683, "equipment capacities reach PTV load dimensions");
Check(equipped["categories"]![0]!.GetValue<string>() == "LIFTGATE" && equipped["costs"]!["perHour"]!.GetValue<double>() == 65, "equipment categories and operating rates reach the request");
Check(equipped["costs"]!["fixed"] is null, "missing catalog rates are not invented");
Reject(() => EquipmentPlanning.Vehicle(equipment, "UNIT1", "DEPOT", "2030-01-01T10:00:00-06:00", "2030-01-01T08:00:00-06:00"), "reversed fleet schedule is rejected");
equipment.PowerOnly = true;
Reject(() => EquipmentPlanning.Vehicle(equipment, "UNIT1", "DEPOT", "2030-01-01T08:00:00-06:00", "2030-01-01T18:00:00-06:00"), "power-only tractors cannot become unlimited cargo vehicles");
equipment.PowerOnly = false; equipment.Weight = -1;
Reject(equipment.Validate, "negative catalog capacity is rejected");
var edited = RoutingInput.Parse(PlannerEdits.Appointment(json, "TEST_NORTH", "2030-01-01T10:00:00-06:00", "2030-01-01T11:00:00-06:00", true, 22.5, false));
Check(RoutingInput.Describe(edited, "TEST_NORTH")!.Windows[0].EarliestStart!.Value.Hour == 10, "appointment editor changes the selected PRO");
Check(edited["orders"]!["deliveries"]![0]!["delivery"]!["duration"]!.GetValue<int>() == 1350, "service minutes become seconds");
Check(RoutingInput.ReportOrder(edited, "TEST_NORTH")!["shipToName"]!.GetValue<string>() == "Synthetic north appointment" && JsonNode.DeepEquals(edited["orders"]!["deliveries"]![1], original["orders"]!["deliveries"]![1]), "appointment edit retains metadata and other orders");
var cleared = RoutingInput.Parse(PlannerEdits.Appointment(json, "TEST_NORTH", null, null, false, null, true));
Check(RoutingInput.Appointment(RoutingInput.ReportOrder(cleared, "TEST_NORTH")) is null && RoutingInput.Appointment(RoutingInput.ReportOrder(original, "TEST_NORTH")) is not null, "clear appointment leaves the original input untouched");
Reject(() => PlannerEdits.Appointment(json, "UNKNOWN", null, null, false, 5, true), "unknown appointment PRO cannot mutate the plan");
Reject(() => PlannerEdits.Appointment(json, "TEST_NORTH", null, null, false, -1, true), "negative service time is rejected");
Console.WriteLine($"{count} total routing, equipment and planner checks passed.");
var nativeEdit = RoutingInput.Parse(PlannerEdits.Appointment(opening.ToJsonString(), "TEST_NORTH", "2030-01-01T07:00:00-06:00", "2030-01-01T07:30:00-06:00", true, 10, false, true));
Check(RoutingInput.Describe(RoutingInput.Parse(RoutingInput.Prepare(nativeEdit.ToJsonString())), "TEST_NORTH")!.Windows[0].EarliestStart!.Value.Hour == 7, "explicit native-window replacement permits a newly chosen appointment");
Check(JsonNode.DeepEquals(nativeEdit["locations"]![1], opening["locations"]![1]), "native-window replacement leaves the original shared location untouched");
var samples = System.Text.Json.JsonSerializer.Deserialize<EquipmentCatalogDto>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"equipment.json")))!;
foreach (var type in samples.Types) type.Validate();
foreach (var unit in samples.Units) unit.Validate();
Check(samples.Types.Count == 14 && samples.Units.Count == 279, "spreadsheet sample covers 14 types and 279 units");
Check(samples.Units.Select(x => x.Id).Distinct().Count() == 279 && samples.Units.All(x => samples.Types.Any(t => t.Code == x.TypeCode)), "sample unit IDs are unique and reference known types");
Console.WriteLine($"{count} final checks passed.");

// A real result shape has depot pickup tasks as well as deliveries; input routes use a flat sequence.
var reusable = RoutingInput.Parse("""
{"status":"SUCCEEDED","routes":[{"vehicleId":"TEST_TRUCK_1","start":{"start":"2030-01-01T08:00:00-06:00"},"stops":[
{"appointments":[{"tasks":[{"orderId":"TEST_NORTH","type":"PICKUP","depotId":"TEST_DEPOT","duration":0}]}]},
{"appointments":[{"timeSlotId":"OLD_SLOT","tasks":[{"orderId":"TEST_NORTH","type":"DELIVERY","start":"2030-01-01T09:00:00-06:00","duration":900}]}]}]}]}
""");
var quick = RoutingInput.Parse(QuickUpdate.Prepare(RoutingInput.Prepare(edited.ToJsonString()), reusable.ToJsonString()));
var seed = quick["routes"]![0]!;
Check(quick["settings"]!["duration"]!.GetValue<int>() == 5 && edited["settings"]!["duration"]!.GetValue<int>() == 1, "quick budget does not overwrite saved full budget");
Check(seed["tasks"]!.AsArray().Count == 2 && seed["tasks"]![0]!["type"]!.GetValue<string>() == "PICKUP", "pickup and delivery order is retained in a flat input route");
Check(seed["tasks"]![1]!["timeSlotId"]!.GetValue<string>() == "APPT_0", "quick update maps old appointment slots onto newly prepared constraints");
Check(seed["tasks"]![1]!["start"] is null && seed["tasks"]![1]!["duration"] is null && seed["stops"] is null, "old timings and result-only fields do not leak into input routes");
Check(seed["reconstructionPolicy"]!["violations"]!.GetValue<string>() == "CLEANUP", "PTV must reconstruct against current constraints");
Check(quick["orders"]!["deliveries"]!.AsArray().Count == 3 && quick["orders"]!["deliveries"]![0]!["delivery"]!["duration"]!.GetValue<int>() == 1350, "new service duration and previously unscheduled orders remain in quick input");
Check(RoutingInput.Describe(quick, "TEST_NORTH")!.Windows[0].EarliestStart!.Value.Hour == 10, "changed appointment is enforced before routes are added");
var noTruck = RoutingInput.Parse(preparedText);
noTruck["vehicles"] = new JsonArray();
Check(RoutingInput.Parse(QuickUpdate.Prepare(noTruck.ToJsonString(), reusable.ToJsonString()))["routes"]!.AsArray().Count == 0, "removed vehicles are not seeded");
var noOrder = RoutingInput.Parse(preparedText);
noOrder["orders"]!["deliveries"]!.AsArray().RemoveAt(0);
Check(RoutingInput.Parse(QuickUpdate.Prepare(noOrder.ToJsonString(), reusable.ToJsonString()))["routes"]!.AsArray().Count == 0, "removed orders drop both pickup and delivery tasks");
var noDepot = RoutingInput.Parse(preparedText);
noDepot["depots"] = new JsonArray();
Check(RoutingInput.Parse(QuickUpdate.Prepare(noDepot.ToJsonString(), reusable.ToJsonString()))["routes"]!.AsArray().Count == 0, "removed depot drops the whole seeded order for reassignment");
var newDate = RoutingInput.Parse(preparedText);
newDate["vehicles"]![0]!["start"]!["earliestStartTime"] = "2030-01-02T07:00:00-06:00";
Check(RoutingInput.Parse(QuickUpdate.Prepare(newDate.ToJsonString(), reusable.ToJsonString()))["routes"]![0]!["start"]!.GetValue<string>().StartsWith("2030-01-02"), "changed shift date replaces previous route date");
Reject(() => QuickUpdate.Prepare(preparedText, """{"status":"FAILED"}"""), "failed results cannot seed another run");
Reject(() => QuickUpdate.Prepare(quick.ToJsonString(), reusable.ToJsonString()), "explicit preassigned routes are not silently replaced");
var duplicateRoute = RoutingInput.Parse(reusable.ToJsonString());
duplicateRoute["routes"]!.AsArray().Add(duplicateRoute["routes"]![0]!.DeepClone());
Reject(() => QuickUpdate.Prepare(preparedText, duplicateRoute.ToJsonString()), "duplicate vehicle seeds are rejected before PTV");
Console.WriteLine($"{count} checks including quick updates passed.");

// Exercise the service/HTTP boundary without credentials or paid PTV calls.
var portProbe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
portProbe.Start();
var testPort = ((System.Net.IPEndPoint)portProbe.LocalEndpoint).Port;
portProbe.Stop();
using var listener = new System.Net.HttpListener();
listener.Prefixes.Add($"http://localhost:{testPort}/");
listener.Start();
var submitted = new List<JsonObject>();
var stub = Task.Run(async () =>
{
    for (var i = 0; i < 4; i++)
    {
        var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(15));
        var body = reusable.ToJsonString();
        if (context.Request.HttpMethod == "POST")
        {
            using var reader = new StreamReader(context.Request.InputStream);
            submitted.Add(RoutingInput.Parse(await reader.ReadToEndAsync()));
            body = """{"id":"synthetic-test"}""";
            context.Response.StatusCode = 202;
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
});
var service = new OptimizationService(Microsoft.Extensions.Options.Options.Create(new PtvSettings
{
    BaseUrl = $"http://localhost:{testPort}", ApiKey = "synthetic-test-only", PollIntervalSeconds = 1
}));
using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
var fullResult = await service.OptimizeAsync("test.json", edited.ToJsonString(), testTimeout.Token);
var quickResult = await service.OptimizeAsync("test.json", edited.ToJsonString(), testTimeout.Token, fullResult.RawResponse);
await stub;
Check(submitted.Count == 2 && submitted.All(x => x["reporting"] is null), "full and quick requests remove reporting metadata before HTTP submission");
Check(submitted[0]["routes"] is null && submitted[0]["settings"]!["duration"]!.GetValue<int>() == 1, "full optimization retains configured budget and fresh input");
Check(submitted[1]["routes"]!.AsArray().Count == 1 && submitted[1]["settings"]!["duration"]!.GetValue<int>() == 5, "quick service submits reusable routes with the short budget");
Check(quickResult.Status == "SUCCEEDED" && quickResult.Log.Any(x => x.StartsWith("Quick update:")), "quick results retain status and identify the seeded run");
Check(Aurora.Client.ProductWorkspace.RouteFor("auroratms") == "/workspace/auroratms" &&
    Aurora.Client.ProductWorkspace.CodeFor("/workspace/auroratms") == "auroratms" &&
    Aurora.Client.ProductWorkspace.NameFor("auroratms") == "Aurora TMS", "TMS launcher opens and highlights its own workspace");
var authorizeReturn = "/connect/authorize?client_id=auroratms-spa&state=original";
Check(Aurora.Client.AuthenticationNavigation.AuthorizationReturnPath(authorizeReturn) == authorizeReturn,
    "login resumes the original product authorization request");
foreach (var unsafeReturn in new[] { "https://untrusted.example/connect/authorize", "//untrusted.example", "/\\untrusted.example", "/login", "/connect/authorize/../login" })
    Check(Aurora.Client.AuthenticationNavigation.AuthorizationReturnPath(unsafeReturn) is null,
        "login refuses unsafe return path " + unsafeReturn);
Console.WriteLine($"{count} checks including HTTP request flow passed.");
