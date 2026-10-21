using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Aurora.Api.Features.Routing;
using Aurora.Contracts;
using Aurora.Modules.Routing.Optimization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var count = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
int Status(IResult r) => ((IStatusCodeHttpResult)r).StatusCode ?? 200;
PlanningSessionDto Value(IResult r) => (PlanningSessionDto)((IValueHttpResult)r).Value!;
var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample.json"));
var input = RoutingInput.Parse(json);
var firstOrder = RoutingInput.Text(input["orders"]!["deliveries"]![0]!["id"])!;
var truck = RoutingInput.Text(input["vehicles"]![0]!["id"])!;
var location = RoutingInput.Text(input["orders"]!["deliveries"]![0]!["delivery"]!["locationId"])!;
var previous = new JsonObject { ["status"] = "SUCCEEDED", ["routes"] = new JsonArray(new JsonObject {
    ["vehicleId"] = truck, ["stops"] = new JsonArray(new JsonObject { ["locationId"] = location,
        ["appointments"] = new JsonArray(new JsonObject { ["tasks"] = new JsonArray(new JsonObject { ["orderId"] = firstOrder, ["type"] = "DELIVERY" }) }) }) }) };
var draftId = Guid.NewGuid();
var dto = new StartPlanningDto(Guid.NewGuid(), "sample.json", json, previous.ToJsonString(), 120, true, draftId);
var newCosts = (JsonObject)previous.DeepClone();
newCosts["metrics"] = JsonNode.Parse("{\"costs\":{\"grossTotal\":123},\"numberOfScheduledOrders\":1}");
newCosts["routes"]![0]!["metrics"] = JsonNode.Parse("{\"costs\":{\"total\":99}}");
var costSummary = RunSummary.Parse(newCosts.ToJsonString(), json, 1, "test");
Check(costSummary.Record.Cost == 123 && costSummary.Routes.Single().Cost == 99, "final costs match current progress schema");
var prepared = RoutingInput.Parse(PlanningRequest.Prepare(dto));
Check(prepared["settings"]!["duration"]!.GetValue<int>() == 120, "seeded replanning respects chosen budget, not hardcoded 5s");
Check(prepared["routes"]!.AsArray().Count == 1, "previous routes seed the new job");
var pin = prepared["constraints"]!["combinations"]!["orderVehicle"]![0]!;
var category = pin["orderCategory"]!.GetValue<string>();
Check(pin["type"]!.GetValue<string>() == "ORDER_REQUIRES_VEHICLE" && pin["violationCost"] is null, "assignment constraint is hard");
Check(prepared["vehicles"]![0]!["categories"]!.AsArray().Any(v => v!.GetValue<string>() == category), "pin belongs to designated vehicle");
Check(prepared["orders"]!["deliveries"]![0]!["properties"]!["categories"]!.AsArray().Any(v => v!.GetValue<string>() == category), "order requires pinned category");
var removed = RoutingInput.Parse(json); removed["vehicles"]!.AsArray().RemoveAt(0);
try { PlanningRequest.Prepare(dto with { RequestJson = removed.ToJsonString() }); throw new Exception("missing truck accepted"); }
catch (FormatException) { count++; }
Check(RoutingInput.Parse(json)["vehicles"]![0]!["categories"]?.ToJsonString().Contains("AURORA_PIN_") != true, "source remains immutable");

var portProbe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); portProbe.Start();
var port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
var malformedSubmit = false;
var providerCost = 88;
var submits = 0; var stops = 0; var done = false; var rejectStop = false; var rejectPoll = false;
JsonObject? providerRequest = null;
var loop = Task.Run(async () =>
{
    while (listener.IsListening)
    {
        HttpListenerContext context;
        try { context = await listener.GetContextAsync(); } catch { break; }
        var path = context.Request.Url!.AbsolutePath;
        var body = "{}";
        if (path.EndsWith("/stop"))
        {
            stops++;
            if (rejectStop) context.Response.StatusCode = 503;
            else { done = true; context.Response.StatusCode = 202; }
        }
        else if (context.Request.HttpMethod == "POST")
        {
            submits++; using var reader = new StreamReader(context.Request.InputStream);
            providerRequest = RoutingInput.Parse(await reader.ReadToEndAsync());
            context.Response.StatusCode = 202; body = malformedSubmit ? "invalid-json" : "{\"id\":\"provider-job\"}";
        }
        else if (path.EndsWith("/progress"))
            body = "{\"samples\":[{\"time\":\"2026-09-23T12:00:00Z\",\"metrics\":{\"numberOfScheduledOrders\":1,\"numberOfUnscheduledOrders\":2,\"numberOfRoutes\":1,\"costs\":{\"grossTotal\":88}}}]}";
        else
        {
            if (rejectPoll) context.Response.StatusCode = 503;
            var result = (JsonObject)previous.DeepClone(); result["id"] = "provider-job"; result["status"] = done ? "SUCCEEDED" : "RUNNING";
            result["metrics"] = JsonNode.Parse("{\"numberOfScheduledOrders\":1,\"numberOfUnscheduledOrders\":2,\"numberOfRoutes\":1,\"totalCost\":88}");
            result["metrics"]!["totalCost"]=providerCost;
            body = result.ToJsonString();
        }
        var bytes = Encoding.UTF8.GetBytes(body); context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
    }
});
var directory = Path.Combine(Path.GetTempPath(), "aurora-session-checks-" + Guid.NewGuid().ToString("N"));
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Routing:SessionPath"] = directory }).Build();
var protection = new EphemeralDataProtectionProvider();
var options = Options.Create(new PtvSettings { ApiKey = "test-only", BaseUrl = $"http://localhost:{port}" });
var store = new PlanningSessions(config, new EnvironmentStub(), protection, options);
ClaimsPrincipal User(string tenant, string subject) => new(new ClaimsIdentity([new Claim("tenant_id", tenant), new Claim("sub", subject)], "test"));
var owner = User("tenant-a", "user-a");
var response = await store.Start(dto, owner);
Check(Status(response) == 200 && Value(response).Status == "QUEUING", "submit returns immediately with a session");
Check(providerRequest!["reporting"] is null, "provider receives no reporting sidecar");
response = await store.Start(dto, owner);
Check(submits == 1 && Status(response) == 200, "duplicate start does not issue another paid job");
Check(Status(await store.Start(dto with { DurationSeconds = 90 }, owner)) == 409, "same session cannot be repurposed");
Check(Status(await store.Poll(dto.Id, User("tenant-b", "user-a"), false, default)) == 404, "other tenant cannot read session");
Check(Status(await store.Poll(dto.Id, User("tenant-a", "user-b"), true, default)) == 404 && stops == 0, "other user cannot stop session");
response = await store.Poll(dto.Id, owner, false, default);
Check(Value(response).Status == "RUNNING" && Value(response).Result is null, "interim state does not become a final result");
Check(Value(response).Samples.Single().Cost == 88, "progress parses current costs.grossTotal");
Check(Value(response).Candidates.Count==1,"complete running result offered as a candidate");
var firstCandidate=Value(response).Candidates[0];
response=await store.Poll(dto.Id,owner,false,default);
Check(Value(response).Candidates.Count==1,"identical polls do not duplicate results");
providerCost=99;
response=await store.Poll(dto.Id,owner,false,default);
Check(Value(response).Candidates.Count==2,"changed result appended without losing earlier choice");
Check(Status(await store.Accept(dto.Id,firstCandidate.Id,User("tenant-b","user-a"),default))==404,"other tenant cannot accept a result");
Check(Status(await store.Accept(dto.Id,"unknown",owner,default))==404,"unknown result cannot be accepted");

rejectPoll = true;
Check(Status(await store.Poll(dto.Id, owner, false, default)) == 502 && submits == 1, "transient poll failure never resubmits");
rejectPoll = false; rejectStop = true;
Check(Status(await store.Poll(dto.Id, owner, true, default)) == 502, "stop rejection stays actionable");
rejectStop = false;
response = await store.Poll(dto.Id, owner, true, default);
Check(Value(response).Status == "STOPPING", "stop is distinct from completed result");
await store.Poll(dto.Id, owner, true, default);
Check(stops == 2, "accepted stop is not sent again");
response = await store.Poll(dto.Id, owner, false, default);
Check(Value(response).Status == "SUCCEEDED" && Value(response).Result is not null, "final result retrieved after stop");
Check((await store.Completed(dto.Id, draftId, owner, default)).Status == "SUCCEEDED", "finish reads the server-stored completed result");
try { await store.Completed(dto.Id, Guid.NewGuid(), owner, default); throw new Exception("wrong draft accepted"); } catch (FormatException) { count++; }
try { await store.Completed(dto.Id, draftId, User("tenant-b", "user-a"), default); throw new Exception("other tenant accepted"); } catch (FormatException) { count++; }
response=await store.Accept(dto.Id,firstCandidate.Id,owner,default);
Check(Value(response).AcceptedCandidateId==firstCandidate.Id && Value(response).Result!.Summary!.Cost==88,"accepting earlier result uses its exact stored routes and metrics");
Check((await store.Completed(dto.Id,draftId,owner,default)).Summary!.Cost==88,"manifest creation uses selected result");
Check(Status(await store.Accept(dto.Id,firstCandidate.Id,owner,default))==200,"accept is idempotent");
Check(Status(await store.Accept(dto.Id,"other",owner,default))==409,"later selection cannot replace accepted plan");
var recovered = new PlanningSessions(config, new EnvironmentStub(), protection, options);
response = await recovered.Poll(dto.Id, owner, false, default);
Check(Value(response).Result is not null && submits == 1, "restart recovers final session without resubmission");
Check(!File.ReadAllText(Directory.GetFiles(directory, "*.json").Single()).Contains(firstOrder), "stored shipment data is encrypted");
Check(Value(response).RequestJson == "", "routine polls omit large inputs");
response = await recovered.Poll(dto.Id, owner, false, default, includeInputs: true);
Check(Value(response).RequestJson == json, "refresh restores original inputs on demand");
malformedSubmit = true;
var uncertain = dto with { Id = Guid.NewGuid() };
Check(Status(await store.Start(uncertain, owner)) == 502, "malformed accepted response remains uncertain, not invalid input");
response = await store.Poll(uncertain.Id, owner, false, default, includeInputs: true);
Check(Value(response).Status == "SUBMITTING" && Value(response).RequestJson == json, "uncertain submission retains recoverable inputs");
await store.Start(uncertain, owner);
Check(submits == 2, "uncertain submission is not repeated");
malformedSubmit=false;done=false;
var live=dto with {Id=Guid.NewGuid()};
await store.Start(live,owner);
var liveCandidate=Value(await store.Poll(live.Id,owner,false,default)).Candidates.Single();
rejectStop=true;
Check(Status(await store.Accept(live.Id,liveCandidate.Id,owner,default))==502,"failed stop retains pending selection");
rejectStop=false;
response=await store.Accept(live.Id,liveCandidate.Id,owner,default);
Check(Value(response).Result!.Status=="SUCCEEDED" && Value(response).AcceptedCandidateId==liveCandidate.Id,"running plan can be accepted and stopped");
providerCost=999;
response=await store.Poll(live.Id,owner,false,default);
Check(Value(response).Result!.Summary!.Cost==99,"poll cannot replace accepted snapshot with newer provider result");
// A provider may leave a pinned order unscheduled; it must never silently reach manifests.
var explicitPin = dto with { Id = Guid.NewGuid(), RequestJson = PlanOrderDecisions.Set(json, firstOrder, "truck:TEST_TRUCK_2"), KeepAssignments = true };
var explicitPrepared = RoutingInput.Parse(PlanningRequest.Prepare(explicitPin));
Check(RoutingInput.Items(explicitPrepared["constraints"]?["combinations"]?["orderVehicle"]).Count() == 1, "explicit pin overrides previous KeepAssignments truck");
await store.Start(explicitPin, owner);
done = true;
var pinnedSession = Value(await store.Poll(explicitPin.Id, owner, false, default));
Check(PlanOrderDecisions.UnscheduledPins(explicitPin.RequestJson, pinnedSession.Result!).Contains(firstOrder), "wrong-truck result reports unsatisfied pin");
try { await store.Completed(explicitPin.Id, draftId, owner, default); throw new Exception("unsatisfied pin reached manifests"); }
catch (FormatException) { count++; }
var restoredPin = Value(await recovered.Poll(explicitPin.Id, owner, false, default, includeInputs: true));
Check(PlanOrderDecisions.Get(RoutingInput.Parse(restoredPin.RequestJson), firstOrder) == "truck:TEST_TRUCK_2", "session recovery retains explicit decisions");
Check(PlanOrderDecisions.UnscheduledPins(PlanOrderDecisions.Set(json, firstOrder, "truck:" + truck), pinnedSession.Result!).Count == 0, "satisfied pin permits manifest creation");
var unrelated = dto with { Id = Guid.NewGuid(), WorkspaceDraftId = Guid.NewGuid() };
await store.Start(unrelated, owner);
await store.ResetWorkspace(() => Task.FromResult((new WorkspaceResetResult(1, 1, 1), new[] { draftId })));
Check(Status(await store.Poll(explicitPin.Id, owner, false, default)) == 404, "reset removes sessions belonging to cleared drafts");
Check(Status(await store.Poll(unrelated.Id, owner, false, default)) == 200, "reset preserves sessions for other drafts");
listener.Stop(); await loop;
Directory.Delete(directory, true);
Console.WriteLine($"{count} planning session checks passed.");

sealed class EnvironmentStub : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "checks";
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
