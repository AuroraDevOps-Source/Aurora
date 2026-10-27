using Aurora.Integrations.Nova;
using Aurora.Modules.Routing;
using Aurora.Contracts;
using Aurora.Modules.Routing.Optimization;
using System.Text.Json.Nodes;

var count = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); count++; }
void Reject(Action action, string name) { try { action(); } catch (FormatException) { Check(true, name); return; } throw new Exception("FAIL: " + name); }
Check(NovaOrderSearch.NativeId("FB-11404") == 11404, "displayed fallback order ID is searchable");
Check(NovaOrderSearch.NativeId(" fb11404 ") == 11404, "fallback order search accepts case, spaces and omitted hyphen");
Check(new string?[] { null, "", "FB-", "FB--1", "FB-0", "FB-999999999999999999999", "FB trucking", "11404" }.All(s => NovaOrderSearch.NativeId(s) is null), "ordinary text and invalid aliases do not become native IDs");
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

foreach(var profile in EquipmentRoutingProfiles.Choices.Keys)
{
    EquipmentRoutingProfiles.Validate(profile);
    var fleet=FirebirdRoutingSource.MapFleet([new(){NativeId=901,Unit="PROFILE-TRUCK",Description="Test vehicle",Terminal="TEST",Weight=12000,Cubes=1700,PerHour=25,PerMile=1.609344}],"-06:00");
    fleet.Types[0].RoutingProfile=profile;
    var profileRequest=RoutingInput.Parse(json);
    profileRequest["reporting"]!["terminal"]="TEST";
    var built=FleetPlanning.Build(profileRequest.ToJsonString(),fleet);
    var rebuilt=RoutingInput.Parse(FleetPlanning.ApplySavedFleet(built,fleet,"TEST"));
    rebuilt["vehicles"]![0]!["routing"]!["profile"]="CLIENT-OVERRIDE";
    var trusted=FleetPlanning.ApplySavedFleet(rebuilt.ToJsonString(),fleet,"TEST");
    var outgoing=RoutingInput.Parse(ManifestReportExtractor.RemoveReportingSidecar(RoutingInput.Prepare(trusted)));
    var vehicle=RoutingInput.Items(outgoing["vehicles"]).Single();
    Check(RoutingInput.Text(vehicle["routing"]?["profile"])==profile,$"saved profile reaches PTV and overrides client edits: {profile}");
    Check(vehicle["costs"]!["perHour"]!.GetValue<double>()==25 && vehicle["costs"]!["perKilometer"]!.GetValue<double>()==1 &&
        RoutingInput.Items(vehicle["constraints"]?["maximumLoads"]).Any(l=>RoutingInput.Text(l["dimension"])=="weight" && l["value"]!.GetValue<double>()==12000),"profile preserves capacity and costs");
}
Reject(()=>EquipmentRoutingProfiles.Validate("invented-profile"),"unsupported profile rejected");
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

var equipment = new EquipmentTypeDto { Code = "BOX26", Description = "26 foot box truck", Weight = 12000, Cubes = 1683, Categories = "LIFTGATE", MaximumStops = 12, PerHour = 65, PerKilometer = 0 };
var equipped = EquipmentPlanning.Vehicle(equipment, "UNIT1", "DEPOT", "2030-01-01T22:00:00-06:00", "2030-01-02T08:00:00-06:00");
Check(equipped["end"]!["latestEndTime"]!.GetValue<string>().Contains("2030-01-02"), "equipment supports overnight shifts without changing the date or offset");
Check(equipped["constraints"]!["maximumLoads"]![0]!["value"]!.GetValue<double>() == 12000 && equipped["constraints"]!["maximumLoads"]![1]!["value"]!.GetValue<double>() == 1683, "equipment capacities reach PTV load dimensions");
Check(equipped["categories"]![0]!.GetValue<string>() == "LIFTGATE" && equipped["costs"]!["perHour"]!.GetValue<double>() == 65, "equipment categories and operating rates reach the request");
Check(equipped["costs"]!["fixed"] is null, "missing catalog rates are not invented");
Check(equipped["costs"]!["perKilometer"]!.GetValue<double>() == 0, "explicit zero distance rate accepted");
Check(DataRequirements.VehicleCosts(null,null).Length==2, "blank rates produce two warnings");
Check(DataRequirements.VehicleCosts(0,0).Length==0, "zero rates produce no warning");
var noRates = new EquipmentTypeDto { Code="NOVA", Description="Nova truck" };
Reject(()=>EquipmentPlanning.Vehicle(noRates,"NOVA-1","DEPOT","2030-01-01T08:00:00-06:00","2030-01-01T18:00:00-06:00"),"blank rates cannot be submitted");
var rateFleet=FirebirdRoutingSource.MapFleet([
    new(){NativeId=1,Unit="ZERO",Terminal="MHT",Description="Truck",Weight=100,Cubes=100,PerHour=0,PerMile=0},
    new(){NativeId=2,Unit="BLANK",Terminal="MHT",Description="Truck",Weight=100,Cubes=100},
    new(){NativeId=3,Unit="PAID",Terminal="MHT",Description="Truck",Weight=100,Cubes=100,PerHour=42,PerMile=1.609344}
],"-04:00");
Check(FleetPlanning.Eligible(rateFleet,"MHT").Select(u=>u.Id).SequenceEqual(new[]{"PAID","ZERO"}),"blank rates excluded and zero rates eligible");
Check(rateFleet.Types.Single(t=>t.Code=="NOVA-3").PerKilometer==1,"miles converted to kilometers without changing hourly costs");
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
Check(quick["settings"]!["duration"]!.GetValue<int>() == 10 && edited["settings"]!["duration"]!.GetValue<int>() == 1, "quick budget does not overwrite saved full budget");
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
Check(submitted[1]["routes"]!.AsArray().Count == 1 && submitted[1]["settings"]!["duration"]!.GetValue<int>() == 10, "quick service submits reusable routes with the short budget");
Check(quickResult.Status == "SUCCEEDED" && quickResult.Log.Any(x => x.StartsWith("Quick update:")), "quick results retain status and identify the seeded run");
Check(Aurora.Client.ProductWorkspace.RouteFor("auroratms") == "/workspace/auroratms" &&
    Aurora.Client.ProductWorkspace.CodeFor("/workspace/auroratms") == "auroratms" &&
    Aurora.Client.ProductWorkspace.NameFor("auroratms") == "Nova 2.0", "TMS launcher opens and highlights its own workspace");
var authorizeReturn = "/connect/authorize?client_id=auroratms-spa&state=original";
Check(Aurora.Client.AuthenticationNavigation.AuthorizationReturnPath(authorizeReturn) == authorizeReturn,
    "login resumes the original product authorization request");
foreach (var unsafeReturn in new[] { "https://untrusted.example/connect/authorize", "//untrusted.example", "/\\untrusted.example", "/login", "/connect/authorize/../login" })
    Check(Aurora.Client.AuthenticationNavigation.AuthorizationReturnPath(unsafeReturn) is null,
        "login refuses unsafe return path " + unsafeReturn);
Console.WriteLine($"{count} checks including HTTP request flow passed.");

var customerInput = RoutingInput.Parse(json);
var extraOrder = (JsonObject)customerInput["orders"]!["deliveries"]![0]!.DeepClone();
extraOrder["id"] = "SECOND_NORTH";
customerInput["orders"]!["deliveries"]!.AsArray().Add(extraOrder);
customerInput["reporting"]!["orders"]!["SECOND_NORTH"] = customerInput["reporting"]!["orders"]!["TEST_NORTH"]!.DeepClone();
customerInput["reporting"]!["orders"]!["TEST_SOUTH"]!["shipToName"] = "Synthetic north appointment";
var customerJson = customerInput.ToJsonString();
var customers = PlanningCustomers.Read(customerJson);
var northCustomer = customers.Single(c => c.OrderIds.Contains("SECOND_NORTH"));
Check(northCustomer.OrderIds.Count == 2 && customers.Count == 3, "customer grouping joins multiple PROs but separates same-name destinations");
var customerUpdated = PlannerEdits.CustomerAppointment(customerJson, northCustomer.Key, "2030-01-01T10:00:00-06:00", "2030-01-01T11:00:00-06:00", true, 20, false);
var customerRoot = RoutingInput.Parse(customerUpdated);
Check(northCustomer.OrderIds.All(id => RoutingInput.Describe(customerRoot, id)!.Windows[0].EarliestStart!.Value.Hour == 10), "customer appointment updates every selected order at the destination");
Check(RoutingInput.Describe(customerRoot, "TEST_SOUTH")!.Windows[0].EarliestStart!.Value.Hour == 9, "customer appointment leaves other destinations unchanged");
var customerDeleted = RoutingInput.Parse(PlannerEdits.CustomerAppointment(customerUpdated, northCustomer.Key, null, null, false, null, true));
Check(northCustomer.OrderIds.All(id => RoutingInput.Appointment(RoutingInput.ReportOrder(customerDeleted, id)) is null), "delete removes the customer appointment from every linked order");
Check(RoutingInput.Describe(customerInput, "TEST_NORTH")!.Windows[0].EarliestStart!.Value.Hour == 9, "customer edits leave original inputs unchanged");
var isolated = PlannerEdits.CustomerAppointment(customerJson, northCustomer.Key, "2030-01-01T10:00:00-06:00", "2030-01-01T11:00:00-06:00", true, null, false, true);
Check(PlanningCustomers.Read(isolated).Single(c => c.Key == northCustomer.Key).OrderIds.Count == 2, "customer identity survives isolated location copies");
Reject(() => PlannerEdits.CustomerAppointment(customerJson, northCustomer.Key, "2030-01-01T12:00:00-06:00", "2030-01-01T11:00:00-06:00", true, null, false), "invalid customer window is rejected");
var shiftInput = RoutingInput.Parse(json);
shiftInput["reporting"]!["unavailableVehicles"] = new JsonArray(shiftInput["vehicles"]![1]!.DeepClone());
shiftInput["vehicles"]!.AsArray().RemoveAt(1);
var zone = RoutingInput.Text(shiftInput["vehicles"]![0]!["start"]!["earliestStartTime"])![^6..];
var shipDay = RoutingInput.Parse(PlannerEdits.FleetDay(shiftInput.ToJsonString(), new DateOnly(2031, 3, 4), "06:00", "18:00"));
Check(RoutingInput.Items(shipDay["vehicles"]).Concat(RoutingInput.Items(shipDay["reporting"]?["unavailableVehicles"])).All(t => RoutingInput.Text(t["start"]?["earliestStartTime"]) == $"2031-03-04T06:00:00{zone}" && RoutingInput.Text(t["end"]?["latestEndTime"]) == $"2031-03-04T18:00:00{zone}"), "ship date and time window apply to every truck in its own offset");
Check(shipDay["vehicles"]!.AsArray().Count == 1 && JsonNode.DeepEquals(shipDay["vehicles"]![0]!["costs"], shiftInput["vehicles"]![0]!["costs"]), "ship-day schedule preserves availability and truck costs");
var overnightDay = RoutingInput.Parse(PlannerEdits.FleetDay(json, new DateOnly(2031, 3, 4), "20:00", "05:00"));
Check(RoutingInput.Items(overnightDay["vehicles"]).All(t => RoutingInput.Text(t["end"]?["latestEndTime"]) == $"2031-03-05T05:00:00{zone}"), "time window ending before departure returns the next day");
Reject(() => PlannerEdits.FleetDay(json, new DateOnly(2031, 3, 4), "06:00", "06:00"), "equal departure and return rejected");
Reject(() => PlannerEdits.FleetDay(json, new DateOnly(2031, 3, 4), "6am", "18:00"), "malformed truck time rejected");
Console.WriteLine($"{count} checks including customer appointments and overall schedules passed.");

// Transportation network: service areas, planning batches, and routing to a node instead of the consignee.
var area = ServiceArea.Parse(string.Join(Environment.NewLine, "917, 923-925", "90001-90099"));
Check(area.Contains("91761") && area.Contains("92410") && area.Contains("90044") && !area.Contains("92601") && !area.Contains("90100") && !area.Contains(""), "service area matches prefixes and ranges");
Reject(() => ServiceArea.Parse("917-91"), "malformed service area range rejected");
TerminalDto Node(string code, string zip, string region, string role, string area, double lat) => new() { Id = Guid.NewGuid(), Code = code, Name = code + " node", PostalCode = zip, Region = region, RoutingRole = role, ServiceArea = area, Latitude = lat, Longitude = -118 };
var ont = Node("ONT", "91761", "SOCAL", "Spoke", "917, 923-925", 34.0);
var lax = Node("LAX", "90744", "SOCAL", "Hub", "900-908", 33.8);
var sac = Node("SAC", "95838", "NORCAL", "Hub", "956-958", 38.6);
var transferInput=LineHaulPlanning.Prepare(json,ont,lax);
var transfer=RoutingInput.Parse(LineHaulPlanning.SetFleetDestination(transferInput));
Check(RoutingInput.Items(transfer["orders"]?["deliveries"]).All(o=>RoutingInput.Text(o["delivery"]?["locationId"])=="NODE-LAX"),"explicit line haul sends all selected orders to receiving terminal");
Check(RoutingInput.Items(transfer["vehicles"]).All(v=>RoutingInput.Text(v["end"]?["locationId"])=="NODE-LAX"),"line haul vehicles end at receiving terminal");
Check(RoutingInput.Items(transfer["orders"]?["deliveries"]).All(o=>RoutingInput.ReportOrder(transfer,RoutingInput.Text(o["id"])!)?["appointment"] is null),"final delivery appointments do not constrain terminal transfer");
_ = RoutingInput.Prepare(transfer.ToJsonString());
Check(true,"terminal transfer produces valid optimizer input");
Reject(()=>LineHaulPlanning.Prepare(json,ont,ont),"line haul rejects same terminal");
Reject(()=>LineHaulPlanning.Prepare(json,ont,new TerminalDto{Id=Guid.NewGuid(),Code="MISSING"}),"line haul rejects missing destination coordinates");
var fat = Node("FAT", "93706", "NORCAL", "Spoke", "932, 936-937", 36.7);
var agent = Node("AGT", "92410", "", "", "", 34.1);
agent.NodeType = "Agent Facility";
var network = new[] { ont, lax, sac, fat, agent };
var consigneeTerminal = new TerminalDto { Id=Guid.NewGuid(), Code="100", Address="601 Armory Rd", City="Clarksburg", State="WV", PostalCode="26301", Latitude=39, Longitude=-80 };
var novaOrder = new TmsOrder { Id="FB-1371359", UseConsigneeAddress=true, Address="1637 Greystone Rd", City="Charleston", State="WV", PostalCode="25314", DeliverToNodeId=consigneeTerminal.Id };
Check(PlanningBatches.Classify(novaOrder,ont,[ont,consigneeTerminal]) is { Batch:PlanningBatch.General, DestinationNodeId:null },
    "Nova consignee delivery ignores hidden delivery-terminal metadata");
novaOrder.DeliverToNodeId=Guid.NewGuid();
Check(PlanningBatches.Classify(novaOrder,ont,[ont,consigneeTerminal]).Batch==PlanningBatch.General,
    "stale hidden Nova destination does not block customer delivery");
novaOrder.Address=" 601 ARMORY ROAD. "; novaOrder.City="CLARKSBURG"; novaOrder.PostalCode="26301-7425";
Check(PlanningBatches.Classify(novaOrder,ont,[ont,consigneeTerminal]) is { Batch:PlanningBatch.General, DestinationNodeId:null },
    "matching terminal address does not automatically enable line haul");
novaOrder.ReceivingTerminal="100";
Check(PlanningBatches.Classify(novaOrder,ont,[ont,consigneeTerminal]) is { Batch:PlanningBatch.LineHaul, DestinationNodeId:var matched } && matched==consigneeTerminal.Id,
    "explicit receiving terminal enables line haul");
Reject(()=>PlanningBatches.Classify(novaOrder,consigneeTerminal,[ont,consigneeTerminal]),"line haul cannot target starting terminal");
novaOrder.ReceivingTerminal="MISSING";
Reject(()=>PlanningBatches.Classify(novaOrder,ont,[ont,consigneeTerminal]),"missing explicit receiving terminal blocks planning");
novaOrder.ReceivingTerminal=null;
Check(PlanningBatches.Classify(novaOrder,ont,[ont,consigneeTerminal]).DestinationNodeId is null,"unchecking line haul restores consignee delivery");
var novaSelectionSource=Guid.NewGuid();
var novaSelections=new[] {
    new TmsOrder { SourceId=novaSelectionSource,Id="NOVA-LOCAL",UseConsigneeAddress=true,DeliverToNodeId=consigneeTerminal.Id },
    new TmsOrder { SourceId=novaSelectionSource,Id="NOVA-TRANSFER",UseConsigneeAddress=true,ReceivingTerminal=consigneeTerminal.Code }
};
var novaGroups=PlanningBatches.GroupPlans(new CreateOrderDraftDto(novaSelectionSource,novaSelections.Select(o=>o.Id).ToArray()),novaSelections,ont,[ont,consigneeTerminal]);
Check(novaGroups.Single(g=>g.ToTerminalId is null).OrderIds.SequenceEqual(["NOVA-LOCAL"]),"hidden destination remains in local plan");
Check(novaGroups.Single(g=>g.ToTerminalId==consigneeTerminal.Id).OrderIds.SequenceEqual(["NOVA-TRANSFER"]),"only explicitly checked line haul enters receiving-terminal plan");
Check(novaGroups.All(g=>g.SourceId==novaSelectionSource),"line haul grouping preserves selected starting source");
novaOrder.Address="603 Armory Rd";
Check(PlanningBatches.Classify(novaOrder,ont,[ont,consigneeTerminal]).DestinationNodeId is null,
    "same ZIP and street with different house number remains consignee delivery");
novaOrder.PostalCode="90044";
Check(PlanningBatches.Classify(novaOrder,ont,network).DestinationNodeId is null,
    "Nova customer address is not redirected by another terminal service area");
BatchAssignment Batch(string zip, string service = "", Guid? deliverTo = null) => PlanningBatches.Classify(new TmsOrder { Id = "P-" + zip, PostalCode = zip, ServiceType = service, DeliverToNodeId = deliverTo }, ont, network);
Check(Batch("91761") is { Batch: PlanningBatch.General, DestinationNodeId: null }, "local consignee is general routing");
Check(Batch("92410", OrderServiceTypes.Consolidation) is { Batch: PlanningBatch.Consolidation }, "local consolidation job is its own batch");
Check(Batch("90044") is { Batch: PlanningBatch.LineHaul } b1 && b1.DestinationNodeId == lax.Id, "other service area in the region is line haul to that node");
Check(Batch("93650") is { Batch: PlanningBatch.LineHaul } b2 && b2.DestinationNodeId == fat.Id, "without a lane rule out-of-region freight goes directly to its destination");
Check(Batch("59101") is { Batch: PlanningBatch.General, Reason: "Outside every service area" }, "freight outside every service area stays in general routing");
Check(Batch("91761", deliverTo: agent.Id) is { Batch: PlanningBatch.General } b3 && b3.DestinationNodeId == agent.Id, "deliver-to node inside the service area routes to the node");
Check(Batch("91761", deliverTo: fat.Id) is { Batch: PlanningBatch.LineHaul } b4 && b4.DestinationNodeId == fat.Id, "explicit destination goes direct without a lane rule");
var redirected = RoutingInput.Parse(PlanningBatches.ApplyDestinations(json, new Dictionary<string, TerminalDto> { ["TEST_NORTH"] = lax }));
var nearbyTerminal = Node("NEAR", "91762", "SOCAL", "Spoke", "", 34.01);
Check(PlanningBatches.Classify(new TmsOrder { Id="NEAR-ORDER", PostalCode="91761", DeliverToNodeId=nearbyTerminal.Id }, ont, [..network, nearbyTerminal]) is { Batch: PlanningBatch.LineHaul, DestinationNodeId: var nearId } && nearId == nearbyTerminal.Id,
    "explicit terminal transfer is line haul even inside origin service area");
var groupSource = Guid.NewGuid();
var groupedOrders = new[] { ("LOCAL", "91761"), ("LAX1", "90044"), ("LAX2", "90045"), ("FAT1", "93650") }
    .Select(x => new TmsOrder { SourceId=groupSource, Id=x.Item1, PostalCode=x.Item2 }).ToArray();
var groupInput = new CreateOrderDraftDto(groupSource, groupedOrders.Select(o=>o.Id).ToArray(), new DateOnly(2026,10,15), "07:00", "19:00");
var groups = PlanningBatches.GroupPlans(groupInput, groupedOrders, ont, network);
Check(groups.Length == 3 && groups.Single(g=>g.ToTerminalId==lax.Id).OrderIds.SequenceEqual(new[]{"LAX1","LAX2"})
    && groups.Single(g=>g.ToTerminalId==fat.Id).OrderIds.SequenceEqual(new[]{"FAT1"})
    && groups.Single(g=>g.ToTerminalId is null).OrderIds.SequenceEqual(new[]{"LOCAL"}), "automatic plans separate local freight and each receiving terminal");
Check(groups.All(g=>g.ShipDate==groupInput.ShipDate && g.DepartAt=="07:00" && g.ReturnBy=="19:00") && groups.Where(g=>g.ToTerminalId is not null).All(g=>g.Batch==PlanningBatch.LineHaul), "automatic transfer groups preserve schedule and line-haul type");
Reject(()=>PlanningBatches.GroupPlans(groupInput with {OrderIds=["LOCAL","LOCAL"]}, groupedOrders, ont, network), "automatic plans reject duplicate orders");
Reject(()=>PlanningBatches.GroupPlans(groupInput with {OrderIds=["MISSING"]}, groupedOrders, ont, network), "automatic plans reject missing orders");
var duplicateArea = Node("ALT", "90044", "SOCAL", "Hub", "900-908", 33.8);
Reject(()=>PlanningBatches.GroupPlans(groupInput, groupedOrders, ont, [..network,duplicateArea]), "ambiguous receiving terminal is not silently chosen");
Reject(()=>PlanningBatches.Classify(new TmsOrder {Id="REMOVED",DeliverToNodeId=Guid.NewGuid()}, ont, network), "missing explicit destination is not silently treated as local");
var consolidation = Node("CON", "91760", "SOCAL", "", "", 34.1);
consolidation.IsConsolidation=true;
var finalTerminal = Node("FINAL", "90044", "SOCAL", "Spoke", "900-908", 33.8);
ont.OutboundRoutes=[new(){DestinationTerminalId=finalTerminal.Id,ViaTerminalId=consolidation.Id}];
var consolidationNetwork = new[] {ont,consolidation,finalTerminal};
var outboundOrder = new TmsOrder {SourceId=groupSource,Id="CONSOL-1",PostalCode="90044",DeliverToNodeId=finalTerminal.Id};
var inboundGroup=PlanningBatches.GroupPlans(new(groupSource,[outboundOrder.Id]),[outboundOrder],ont,consolidationNetwork).Single();
Check(inboundGroup.Batch==PlanningBatch.LineHaul && inboundGroup.ToTerminalId==consolidation.Id,"terminal transfer stays line haul regardless of consolidation terminal flag");
var onwardGroup=PlanningBatches.GroupPlans(new(groupSource,[outboundOrder.Id]),[outboundOrder],consolidation,consolidationNetwork).Single();
Check(onwardGroup.Batch==PlanningBatch.LineHaul && onwardGroup.ToTerminalId==finalTerminal.Id,"received freight leaves consolidation for its next terminal without looping");
Check(PlanningBatches.Classify(new TmsOrder{Id="LOCAL-FINAL",PostalCode="90044",DeliverToNodeId=finalTerminal.Id},finalTerminal,consolidationNetwork).Batch==PlanningBatch.General,"at final terminal freight becomes local delivery");
Check(PlanningBatches.Classify(new TmsOrder{Id="DIRECT-CON",PostalCode="91761",DeliverToNodeId=consolidation.Id},ont,consolidationNetwork).Batch==PlanningBatch.LineHaul,"direct transfer into marked terminal stays line haul");
var otherOrigin=Node("OTHER","10001","EAST","","100",40);
var laneNetwork=new[]{ont,otherOrigin,consolidation,finalTerminal,lax};
Check(PlanningBatches.Classify(outboundOrder,otherOrigin,laneNetwork).DestinationNodeId==finalTerminal.Id,"same destination can go direct from a different origin");
Check(PlanningBatches.Classify(new TmsOrder{Id="OTHER-DEST",DeliverToNodeId=lax.Id},ont,laneNetwork).DestinationNodeId==lax.Id,"same origin uses different paths for different destinations");
Check(PlanningBatches.Classify(new TmsOrder{Id="ZIP-LANE",PostalCode="90044"},ont,consolidationNetwork).DestinationNodeId==consolidation.Id,"ZIP-derived destination uses the origin-specific lane");
finalTerminal.RouteViaNodeId=consolidation.Id;
Check(PlanningBatches.Classify(outboundOrder,otherOrigin,laneNetwork).DestinationNodeId==finalTerminal.Id,"retired global via setting no longer overrides origin rules");
otherOrigin.OutboundRoutes=[new(){DestinationTerminalId=finalTerminal.Id,ViaTerminalId=lax.Id}];
Check(PlanningBatches.Classify(outboundOrder,otherOrigin,laneNetwork).DestinationNodeId==lax.Id,"two origins can use different intermediate terminals for one destination");
consolidation.OutboundRoutes=[new(){DestinationTerminalId=finalTerminal.Id,ViaTerminalId=ont.Id}];
Reject(()=>TerminalRoutingRules.Validate(ont,laneNetwork),"multi-terminal cycle for a destination is rejected");
consolidation.OutboundRoutes=[];
ont.OutboundRoutes.Add(new(){DestinationTerminalId=finalTerminal.Id});
Reject(()=>TerminalRoutingRules.Validate(ont,laneNetwork),"duplicate origin-destination rules are rejected");
ont.OutboundRoutes.RemoveAt(1);
ont.OutboundRoutes[0].ViaTerminalId=ont.Id;
Reject(()=>TerminalRoutingRules.Validate(ont,laneNetwork),"rule cannot transfer freight back to its current terminal");
ont.OutboundRoutes[0].ViaTerminalId=Guid.NewGuid();
Reject(()=>TerminalRoutingRules.Validate(ont,laneNetwork),"missing intermediate terminal is rejected");
ont.OutboundRoutes[0].ViaTerminalId=consolidation.Id;
otherOrigin.OutboundRoutes=[];
ont.OutboundRoutes=[];
var consolidationJson=LineHaulPlanning.Prepare(json,ont,consolidation);
Check(RoutingInput.Text(RoutingInput.Parse(consolidationJson)["reporting"]?["transferKind"])==PlanningBatch.LineHaul,"terminal setting does not create a consolidation manifest");
Check(RoutingInput.Text(RoutingInput.Parse(LineHaulPlanning.RestoreDestination(consolidationJson,json))["reporting"]?["transferKind"])==PlanningBatch.LineHaul,"reoptimization restores saved transfer type");
Check(RoutingInput.Text(RoutingInput.Items(redirected["orders"]!["deliveries"]).Single(d => RoutingInput.Text(d["id"]) == "TEST_NORTH")["delivery"]!["locationId"]) == "NODE-LAX"
    && RoutingInput.Items(redirected["locations"]).Any(l => RoutingInput.Text(l["id"]) == "NODE-LAX")
    && RoutingInput.Text(redirected["reporting"]!["orders"]!["TEST_NORTH"]!["deliverToNode"]) == "LAX", "redirected order is delivered to the node location");
Check(RoutingInput.Text(RoutingInput.Items(redirected["orders"]!["deliveries"]).Single(d => RoutingInput.Text(d["id"]) == "TEST_SOUTH")["delivery"]!["locationId"]) != "NODE-LAX", "other orders keep their consignee location");
Reject(() => PlanningBatches.ApplyDestinations(json, new Dictionary<string, TerminalDto> { ["TEST_NORTH"] = new() { Code = "NOPOS", Name = "No position" } }), "node without coordinates cannot receive orders");
Reject(() => new TerminalDto { Code = "HUB", Name = "Hub", RoutingRole = "Hub" }.Validate(), "hub without a service area rejected");
Check(PlanningBatch.For(null, [Batch("91761"), Batch("91762")]) == PlanningBatch.General && PlanningBatch.For(null, [Batch("91761"), Batch("90044")]) == PlanningBatch.Mixed && PlanningBatch.For(PlanningBatch.LineHaul, [Batch("90044")]) == PlanningBatch.LineHaul, "unfiltered plans are named for their one batch, else mixed");
Check(!RoutingInput.Items(redirected["locations"]).Any(l => RoutingInput.Text(l["id"]) == "NORTH") && RoutingInput.Items(redirected["locations"]).Any(l => RoutingInput.Text(l["id"]) == "SOUTH"), "redirected order's unused consignee location is removed");
Check(RoutingInput.Items(RoutingInput.Parse(PlannerEdits.FitTrafficMode(json))["vehicles"]).All(v => RoutingInput.Text(v["routing"]!["trafficMode"]) == "AVERAGE"), "regional plan keeps average traffic");
var longHaul = PlanningBatches.ApplyDestinations(json, new Dictionary<string, TerminalDto> { ["TEST_NORTH"] = new() { Code = "LAX", Name = "Los Angeles", Latitude = 33.79, Longitude = -118.24 } });
Check(RoutingInput.Items(RoutingInput.Parse(PlannerEdits.FitTrafficMode(longHaul))["vehicles"]).All(v => RoutingInput.Text(v["routing"]!["trafficMode"]) == "CONSTANT"), "plan spanning more than 650 km uses constant traffic");
Console.WriteLine($"{count} checks including transportation network batches passed.");

// Explicit exclusions and pins must survive both full and seeded re-optimization.
var excludedPlan = PlanOrderDecisions.Set(json, "TEST_NORTH", PlanOrderDecisions.Excluded);
var exclusionRequest = new StartPlanningDto(Guid.NewGuid(), "test.json", excludedPlan, reusable.ToJsonString(), 75);
var exclusionPrepared = RoutingInput.Parse(PlanningRequest.Prepare(exclusionRequest));
Check(!RoutingInput.Items(exclusionPrepared["orders"]?["deliveries"]).Any(o => RoutingInput.Text(o["id"]) == "TEST_NORTH")
    && !RoutingInput.Items(exclusionPrepared["routes"]).Any(), "excluded order cannot return through previous route seeds");
Check(RoutingInput.Items(RoutingInput.Parse(excludedPlan)["orders"]?["deliveries"]).Count() == 3, "exclusion keeps original orders available for undo and session restore");
Check(exclusionPrepared["settings"]!["duration"]!.GetValue<int>() == 75, "re-optimization retains the selected calculation budget");
var restoredPlan = PlanOrderDecisions.Set(excludedPlan, "TEST_NORTH", "");
Check(RoutingInput.Items(RoutingInput.Parse(PlanningRequest.Prepare(exclusionRequest with { RequestJson = restoredPlan }))["orders"]?["deliveries"]).Count() == 3, "automatic assignment restores excluded order");
var pinInput = RoutingInput.Parse(json);
var secondTruck = pinInput["vehicles"]![0]!.DeepClone();
secondTruck["id"] = "SECOND_TRUCK";
pinInput["vehicles"]!.AsArray().Add(secondTruck);
var pinnedPlan = PlanOrderDecisions.Set(pinInput.ToJsonString(), "TEST_NORTH", "truck:SECOND_TRUCK");
foreach (var prior in new string?[] { null, reusable.ToJsonString() })
{
    var pinnedPrepared = RoutingInput.Parse(PlanningRequest.Prepare(exclusionRequest with { RequestJson = pinnedPlan, PreviousResult = prior }));
    var constraint = RoutingInput.Items(pinnedPrepared["constraints"]?["combinations"]?["orderVehicle"]).Single();
    var category = RoutingInput.Text(constraint["vehicleCategory"]);
    Check(RoutingInput.Text(constraint["type"]) == "ORDER_REQUIRES_VEHICLE" && constraint["violationCost"] is null
        && pinnedPrepared["vehicles"]![2]!["categories"]!.AsArray().Any(c => RoutingInput.Text(c) == category)
        && pinnedPrepared["orders"]!["deliveries"]![0]!["properties"]!["categories"]!.AsArray().Any(c => RoutingInput.Text(c) == category), "pin produces a hard truck constraint in full and seeded runs");
    Check(!RoutingInput.Items(pinnedPrepared["routes"]).Any(), "old truck assignment is not seeded after moving an order");
    Check(pinnedPrepared["vehicles"]!.AsArray().Count == 3, "new truck is offered during re-optimization");
    Check(RoutingInput.Describe(pinnedPrepared, "TEST_NORTH") is { Enforced: true }, "pinned order retains its appointment constraint");
}
var unavailablePin = RoutingInput.Parse(pinnedPlan);
unavailablePin["vehicles"]!.AsArray().RemoveAt(2);
Reject(() => PlanningRequest.Prepare(exclusionRequest with { RequestJson = unavailablePin.ToJsonString() }), "disabled pinned truck is rejected instead of silently ignoring the pin");
var allExcluded = excludedPlan;
foreach (var order in RoutingInput.Items(original["orders"]?["deliveries"]))
    allExcluded = PlanOrderDecisions.Set(allExcluded, RoutingInput.Text(order["id"])!, PlanOrderDecisions.Excluded);
Reject(() => PlanningRequest.Prepare(exclusionRequest with { RequestJson = allExcluded }), "all-excluded plan has an actionable validation error");
Reject(() => PlanOrderDecisions.Set(json, "UNKNOWN", "exclude"), "unknown order decision rejected");
Reject(() => PlanOrderDecisions.Set(json, "TEST_NORTH", "truck:UNKNOWN"), "unknown pinned truck rejected");
Console.WriteLine($"{count} final checks passed including explicit re-optimization decisions.");

PlanningCandidateDto Candidate(string id, double km, double cost = 1) => new(id, DateTimeOffset.UtcNow,
    new RunSummaryDto("SUCCEEDED", 1, 2, 3, 0, km, 100, cost, 0, 3, 4, cost / 3, km / 3, 3, ""));
var mileCandidates = new[] { Candidate("cheap", 30, 1), Candidate("shortest", 10.001, 100), Candidate("latest", 10.002, 50) };
Check(PlanningCandidateSelection.LeastMiles(mileCandidates)?.Id == "shortest", "automatic selection uses exact least miles, not lowest cost or latest result");
Check(PlanningCandidateSelection.LeastMiles(mileCandidates.Reverse())?.Id == "shortest", "candidate order does not change automatic selection");
Check(PlanningCandidateSelection.LeastMiles([Candidate("b", 10), Candidate("a", 10)])?.Id == "a", "equal-distance selection is deterministic for reconnects");
Check(PlanningCandidateSelection.LeastMiles([Candidate("bad", double.NaN), Candidate("negative", -1), Candidate("infinite", double.PositiveInfinity), Candidate("valid", 15)])?.Id == "valid", "invalid distances cannot win automatic selection");
Check(PlanningCandidateSelection.LeastMiles([]) is null, "no candidates returns no selection");
Console.WriteLine($"{count} checks passed including automatic least-mile selection.");

var routedLeftOffFixture = fullResult with { Routes = [new RouteSummaryDto("TEST_TRUCK_1", 1, 1, 15, 1800, 110, 50, 20, "DEMO", 32400, 2700, 1, 1, 100, 10,
    [new ManifestProDto("TEST_NORTH", 1, "Synthetic north appointment", "", "", "", "", 1, 100, 10, "", 900, 39.15, -94.55)], [])] };
var leftOffRemoved = PlannerLeftOff.Read(excludedPlan, routedLeftOffFixture);
Check(leftOffRemoved.Single(o => o.OrderId == "TEST_NORTH").Reason == "Removed by planner", "removing a routed order moves it to Left off with a planner label");
var resultWithoutRemoved = fullResult with { Routes = [], DroppedOrders = [] };
var restoredLeftOff = PlannerLeftOff.Read(excludedPlan, resultWithoutRemoved);
var removedRow = restoredLeftOff.Single(o => o.OrderId == "TEST_NORTH");
Check(removedRow.ShipToName == "Synthetic north appointment" && removedRow.Latitude is not null && removedRow.Appointment is not null, "removed order details survive re-optimization and session restoration");
Check(PlannerLeftOff.Read(restoredPlan, resultWithoutRemoved).Single(o => o.OrderId == "TEST_NORTH").Reason == "Awaiting re-optimization", "included-again order remains visible while waiting for a new route");
Check(PlannerLeftOff.Read(restoredPlan, routedLeftOffFixture).All(o => o.OrderId != "TEST_NORTH"), "routed order does not appear twice in Left off after undo");
Console.WriteLine($"{count} routing checks passed including Left off actions.");

// Manual assignments allow overtime only on their selected trucks.
var overtimeInput = RoutingInput.Parse(pinnedPlan);
var overtimeTruck = overtimeInput["vehicles"]![2]!.AsObject();
overtimeTruck["constraints"] ??= new JsonObject();
overtimeTruck["constraints"]!["maximumLoads"] = new JsonArray(new JsonObject { ["dimension"] = "weight", ["value"] = 10000 });
overtimeTruck["constraints"]!["route"] = new JsonObject { ["maximumDuration"] = 32400, ["maximumDrivingDuration"] = 28800 };
var overtimeOriginal = overtimeInput.ToJsonString();
foreach (var prior in new string?[] { null, reusable.ToJsonString() })
{
    var overtimePrepared = RoutingInput.Parse(PlanningRequest.Prepare(exclusionRequest with { RequestJson = overtimeOriginal, PreviousResult = prior }));
    var changedTruck = overtimePrepared["vehicles"]![2]!;
    Check(RoutingInput.Time(changedTruck["end"]?["latestEndTime"]) == RoutingInput.Time(overtimeTruck["end"]?["latestEndTime"])!.Value.AddDays(1), "manual truck receives one day of overtime allowance");
    Check(changedTruck["constraints"]?["route"]?["maximumDuration"] is null && changedTruck["constraints"]?["route"]?["maximumDrivingDuration"]!.GetValue<int>() == 28800, "overtime relaxes shift duration but preserves driving limit");
    Check(JsonNode.DeepEquals(changedTruck["constraints"]?["maximumLoads"], overtimeTruck["constraints"]?["maximumLoads"]), "overtime preserves truck capacity");
    Check(JsonNode.DeepEquals(overtimePrepared["vehicles"]![0], overtimeInput["vehicles"]![0]), "other trucks retain their original shifts");
    Check(RoutingInput.Describe(overtimePrepared, "TEST_NORTH") is { Enforced: true }, "overtime preserves confirmed appointments");
    Check(JsonNode.DeepEquals(overtimePrepared["reporting"]?[PlannerOvertime.ShiftKey]?["SECOND_TRUCK"], overtimeTruck), "original truck shift retained for reporting");
}
var twicePinned = PlanOrderDecisions.Set(overtimeOriginal, "TEST_SOUTH", "truck:SECOND_TRUCK");
var twicePrepared = RoutingInput.Parse(PlanOrderDecisions.Prepare(twicePinned));
Check(RoutingInput.Time(twicePrepared["vehicles"]![2]!["end"]?["latestEndTime"]) == RoutingInput.Time(overtimeTruck["end"]?["latestEndTime"])!.Value.AddDays(1), "multiple pins do not stack overtime allowance");
var unpinned = PlanOrderDecisions.Set(overtimeOriginal, "TEST_NORTH", "");
Check(JsonNode.DeepEquals(RoutingInput.Parse(PlanOrderDecisions.Prepare(unpinned))["vehicles"]![2], overtimeTruck), "removing final manual assignment restores normal limits");
var overtimeRoute = routedLeftOffFixture.Routes[0] with { Vehicle = "SECOND_TRUCK", BudgetedSeconds = 32400, EstimatedSeconds = 32400 + 42 * 60, Return = null };
Check(PlannerOvertime.OverShiftMinutes(overtimeOriginal, overtimeRoute) == 42, "route reports 42 minutes over original shift");
Check(PlannerOvertime.OverShiftMinutes(overtimeOriginal, overtimeRoute with { EstimatedSeconds = 32400 }) == 0, "route within shift has no warning");
Check(PlannerOvertime.OverShiftMinutes(overtimeOriginal, overtimeRoute with { EstimatedSeconds = 32000, Return = RoutingInput.Time(overtimeTruck["end"]?["latestEndTime"])!.Value.AddMinutes(17) }) == 17, "late return reports overtime even when route duration is below budget");
var overtimeResponse = reusable.DeepClone().AsObject();
overtimeResponse["routes"]![0]!["vehicleId"] = "SECOND_TRUCK";
var overtimeReport = ManifestReportExtractor.ExtractPtv(overtimeResponse.ToJsonString(), twicePrepared.ToJsonString());
Check(overtimeReport.Routes["SECOND_TRUCK"].BudgetedSeconds == 32400, "manifest retains original nine-hour budget after relaxing the solver shift");
Console.WriteLine($"{count} routing checks passed including manual assignment overtime.");

// Adding one left-off order offers only that order and the existing manifest to the solver.
var oneAdded = PlanOrderDecisions.PreserveLeftOff(
    PlanOrderDecisions.Set(json, "TEST_EARLY", "truck:TEST_TRUCK_1"), routedLeftOffFixture);
Check(PlanOrderDecisions.Get(RoutingInput.Parse(oneAdded), "TEST_SOUTH") == PlanOrderDecisions.LeftOff,
    "unselected left-off order is held out when another order is manually added");
Check(PlanOrderDecisions.Get(RoutingInput.Parse(oneAdded), "TEST_NORTH") == "",
    "existing manifest orders remain eligible without adding new truck pins");
foreach (var prior in new string?[] { null, reusable.ToJsonString() })
{
    var scoped = RoutingInput.Parse(PlanningRequest.Prepare(exclusionRequest with { RequestJson = oneAdded, PreviousResult = prior }));
    Check(RoutingInput.Items(scoped["orders"]?["deliveries"]).Select(o => RoutingInput.Text(o["id"])!).ToHashSet()
        .SetEquals(["TEST_NORTH", "TEST_EARLY"]), "full and seeded runs contain existing stops plus only the selected addition");
}
Check(RoutingInput.Items(RoutingInput.Parse(oneAdded)["orders"]?["deliveries"]).Count() == 3,
    "held orders remain in original request for recovery and later additions");
Check(PlanOrderDecisions.PreserveLeftOff(oneAdded, routedLeftOffFixture) == oneAdded,
    "repeated manual edits preserve the same held orders");
Check(PlannerLeftOff.Read(oneAdded, resultWithoutRemoved).Single(o => o.OrderId == "TEST_SOUTH") is { Reason: "Not added to a truck", Latitude: not null },
    "held orders remain visible with a useful reason and map coordinates after optimization");
var nextAdded = PlanOrderDecisions.Set(oneAdded, "TEST_SOUTH", "truck:TEST_TRUCK_1");
Check(RoutingInput.Items(RoutingInput.Parse(PlanOrderDecisions.Prepare(nextAdded))["orders"]?["deliveries"]).Count() == 3,
    "planner can explicitly add a previously held order on the next run");
Check(PlanOrderDecisions.PreserveLeftOff(json, routedLeftOffFixture) == json,
    "ordinary optimization without manual assignments still considers all orders");
var removedAndAdded = PlanOrderDecisions.PreserveLeftOff(PlanOrderDecisions.Set(oneAdded, "TEST_NORTH", PlanOrderDecisions.Excluded), routedLeftOffFixture);
Check(RoutingInput.Items(RoutingInput.Parse(PlanOrderDecisions.Prepare(removedAndAdded))["orders"]?["deliveries"]).Select(o => RoutingInput.Text(o["id"])).SequenceEqual(["TEST_EARLY"]),
    "explicit removals remain excluded alongside unselected left-off orders");

// A pickup retains its own task semantics throughout editing and reoptimization.
var mixedPickup=RoutingInput.Parse(json);
var collected=mixedPickup["orders"]!["deliveries"]!.AsArray()[0]!.AsObject();
mixedPickup["orders"]!["deliveries"]!.AsArray().Remove(collected);
collected["pickup"]=collected["delivery"]!.DeepClone();collected.Remove("delivery");
mixedPickup["orders"]!["pickups"]=new JsonArray(collected);
var mixedJson=mixedPickup.ToJsonString();
var pickupId=RoutingInput.Text(collected["id"])!;
Check(RoutingInput.Orders(mixedPickup).Count()==3,"mixed input includes pickups and deliveries");
var pickupPrepared=RoutingInput.Parse(RoutingInput.Prepare(mixedJson));
Check(RoutingInput.CustomerTask(RoutingInput.Orders(pickupPrepared).Single(o=>RoutingInput.Text(o["id"])==pickupId))!["timeSlotIds"] is JsonArray,"pickup appointment reaches its pickup task");
Check(Aurora.Client.RoutingDraft.Read(mixedJson).Orders==3,"planner includes pickup in order count");
var pickupEdited=RoutingInput.Parse(PlannerEdits.Appointment(mixedJson,pickupId,null,null,false,22,true));
Check(RoutingInput.Orders(pickupEdited).Single(o=>RoutingInput.Text(o["id"])==pickupId)["pickup"]!["duration"]!.GetValue<int>()==1320,"pickup service time edits loading task");
var excludedPickup=RoutingInput.Parse(PlanOrderDecisions.Prepare(PlanOrderDecisions.Set(mixedJson,pickupId,PlanOrderDecisions.Excluded)));
Check(!RoutingInput.Orders(excludedPickup).Any(o=>RoutingInput.Text(o["id"])==pickupId) && RoutingInput.Orders(excludedPickup).Count()==2,"excluding a pickup preserves deliveries");
Check(PickupCapacity.Peak([(false,80d,8d),(true,80d,8d)])==(80d,8d),"delivery frees capacity for later pickup");
Check(PickupCapacity.Peak([(true,80d,8d),(false,80d,8d)])==(160d,16d),"pickup before delivery counts combined onboard load");
Check(PickupCapacity.Peak([(true,30d,3d),(true,40d,4d)])==(70d,7d),"pickup-only route accumulates freight");
var pickupReused=RoutingInput.Parse(reusable.ToJsonString());
foreach(var task in RoutingInput.Items(pickupReused["routes"]).SelectMany(r=>RoutingInput.Items(r["stops"])).SelectMany(st=>RoutingInput.Items(st["appointments"])).SelectMany(a=>RoutingInput.Items(a["tasks"])).Where(t=>RoutingInput.Text(t["orderId"])==pickupId))
{
    var type=RoutingInput.Text(task["type"]);
    task["type"]=type=="PICKUP"?"DELIVERY":"PICKUP";
}
var reusedPickup=RoutingInput.Parse(QuickUpdate.Prepare(RoutingInput.Prepare(mixedJson),pickupReused.ToJsonString()));
Check(RoutingInput.Items(reusedPickup["orders"]?["pickups"]).Count()==1,"quick update retains pickup order type");
var pickupOnly=RoutingInput.Parse(mixedJson);pickupOnly["orders"]!["deliveries"]=new JsonArray();
Check(Aurora.Client.RoutingDraft.Read(RoutingInput.Prepare(pickupOnly.ToJsonString())).Orders==1,"pickup-only planning accepts appointment inputs");


var pickupResponse=RoutingInput.Parse("""
{"status":"SUCCEEDED","routes":[{"vehicleId":"TEST_TRUCK_1","start":{"departure":"2030-01-01T08:00:00-06:00"},"stops":[
{"locationId":"NORTH","appointments":[{"tasks":[{"orderId":"TEST_NORTH","type":"PICKUP"}]}]},
{"locationId":"DEPOT","appointments":[{"tasks":[{"orderId":"TEST_NORTH","type":"DELIVERY","depotId":"TEST_DEPOT"}]}]}
],"end":{"locationId":"DEPOT","arrival":"2030-01-01T12:00:00-06:00"}}],"unscheduledOrders":[]}
""");
var pickupSeed=RoutingInput.Parse(QuickUpdate.Prepare(RoutingInput.Prepare(mixedJson),pickupResponse.ToJsonString()));
var pickupSeedTasks=RoutingInput.Items(pickupSeed["routes"]).SelectMany(r=>RoutingInput.Items(r["tasks"])).ToArray();
Check(pickupSeedTasks.Length==2 && RoutingInput.Text(pickupSeedTasks[0]["type"])=="PICKUP" && pickupSeedTasks[0]["depotId"] is null,"quick update retains customer pickup as customer task");
Check(RoutingInput.Text(pickupSeedTasks[1]["type"])=="DELIVERY" && RoutingInput.Text(pickupSeedTasks[1]["depotId"])=="TEST_DEPOT","quick update unloads pickup freight at depot");
var pickupReport=ManifestReportExtractor.ExtractPtv(pickupResponse.ToJsonString(),mixedJson);
Check(pickupReport.Routes["TEST_TRUCK_1"].Pros.Single().WorkType=="Pickup","route report labels pickup and omits depot unload duplicate");
var pickupCustomer=PlanningCustomers.Read(mixedJson).Single(c=>c.OrderIds.Contains(pickupId));
Check(pickupCustomer.Name.StartsWith("Pickup"),"appointment lookup distinguishes pickup customers");
var returnRoute=new RouteSummaryDto("329",1,1,1,60,1,0,0,"102",3600,60,1,1,1,1,[new(pickupId,1,"Customer","Street","City","WV","",1,1,1,"",900,39,-80) {WorkType="Pickup"}],[new("DEPOT",39,-80,null,null,true,[])]);
PickupPlanning.ValidateReturn(mixedJson,[returnRoute]);Check(true,"pickup route returning to depot accepted");
Reject(()=>PickupPlanning.ValidateReturn(mixedJson,[returnRoute with {RouteStops=[new("NORTH",39,-80,null,null,false,[pickupId])]}]),"pickup route ending at customer rejected");
var pickupNative=new Aurora.Integrations.Nova.FirebirdRoutingSource.SourceOrder{NativeId=123,StopKind="P"};
Check(pickupNative.Id=="FB-P-123" && new Aurora.Integrations.Nova.FirebirdRoutingSource.SourceOrder{NativeId=123}.Id=="FB-123","pickup and delivery snapshots have separate identities");

var customerPolicy=new CustomerPlanning{EstimatedCubesPerPiece=2.5m};
Check(customerPolicy.PlanningCubes(12,4)==12 && customerPolicy.PlanningCubes(0,4)==10 && customerPolicy.PlanningCubes(null,null) is null,"customer estimates preserve actual cubes and require a piece count");
Reject(()=>new CustomerPlanning{Consolidation=true}.Validate(),"consolidation requires both equipment capacities");
Check(!ConsolidationCapacity.Fits(100,8,10,3,200,10)&&!ConsolidationCapacity.Fits(100,8,101,1,200,10),"either capacity can stop consolidation filling");
var groupedInput="""{"locations":[{"id":"A","latitude":42,"longitude":-71},{"id":"B","latitude":42,"longitude":-71},{"id":"C","latitude":42,"longitude":-71,"stopProperties":{"timeSlots":[{"id":"LATER"}]}}],"orders":{"deliveries":[{"id":"1","delivery":{"locationId":"A"}},{"id":"2","delivery":{"locationId":"B"}},{"id":"3","delivery":{"locationId":"C"}}]},"reporting":{"orders":{"1":{"destinationKey":"STORE","keepTogether":true},"2":{"destinationKey":"STORE","keepTogether":true},"3":{"destinationKey":"STORE","keepTogether":true}}}}""";
var customerGrouped=RoutingInput.Parse(CustomerPlanningRequest.GroupLocations(groupedInput));
var customerTasks=RoutingInput.Orders(customerGrouped).Select(o=>RoutingInput.CustomerTask(o)!).ToArray();
Check(customerTasks[0]["locationId"]!.ToString()==customerTasks[1]["locationId"]!.ToString()&&customerTasks[0]["locationId"]!.ToString()!=customerTasks[2]["locationId"]!.ToString(),"same destination groups compatible stops but preserves conflicting visit restrictions");
Check(RoutingInput.Orders(customerGrouped).Count()==3&&customerGrouped["locations"]![0]!["stopProperties"]!["taskAggregation"]!.ToString()=="REQUIRED","grouping preserves individual orders and enforces requested aggregation");
Check(JsonNode.DeepEquals(customerGrouped,RoutingInput.Parse(CustomerPlanningRequest.GroupLocations(customerGrouped.ToJsonString()))),"customer location grouping is idempotent");
var multi=RoutingInput.Parse(MultiDayPlanning.Apply(json,new(2030,1,1),new(2030,1,3),"18:00",new(10,12,12)));
Check(RoutingInput.Items(multi["vehicles"]).All(v=>v["end"]!["latestEndTime"]!.ToString().StartsWith("2030-01-03")&&v["routing"]!["trafficMode"]!.ToString()=="CONSTANT"&&v["breaks"]!["driving"]![0]!["maximumDrivingDuration"]!.GetValue<int>()==36000),"multi-day trips extend the horizon and set driving and rest limits");
Reject(()=>MultiDayPlanning.Apply(json,new(2030,1,1),new(2030,1,16),"18:00",null),"oversized planning horizon rejected");
var metadata=ManifestNotes.WithTripUntil(ManifestNotes.SetPlanningLock(ManifestNotes.WithConsolidation("Dispatch note",new("STORE","ADDR","VAN",200,20,true,[])),true),new(2030,1,3));
metadata=ManifestNotes.Write(metadata,"Changed note");
Check(ManifestNotes.TripUntil(metadata)==new DateOnly(2030,1,3)&&ManifestNotes.Consolidation(metadata)?.Held==true&&ManifestNotes.OptimizationLocked(metadata)&&ManifestNotes.Read(metadata)=="Changed note","note edits preserve trip reservation and consolidation metadata");
Console.WriteLine($"{count} routing checks passed including scoped manual additions.");
