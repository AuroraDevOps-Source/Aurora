using System.Net;
using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Npgsql;
using Aurora.Modules.Routing;
using Aurora.Modules.Routing.Optimization;
using Microsoft.Extensions.Options;

const string match = """{"locationType":"EXACT_ADDRESS","quality":{"totalScore":97},"referencePosition":{"latitude":42.9171,"longitude":-71.3629}}""";
var handler = new LookupHandler { Body = "{\"locations\":[" + match + "]}" };
var lookup = new MasterAddressLookup(new HttpClient(handler), Options.Create(new PtvSettings { ApiKey="test" }));
var input = new NovaMasterEdit("rev",new() { ["LM_ADDR1"]="14 Liberty Drive",["LM_CITY"]="Londonderry",["LM_STATE"]="NH",["LM_ZIP"]="03053",["LM_COUNTRY"]="US",["LM_LATITUDE"]="0",["LM_LONGITUDE"]="0" });
void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
var selectedAccount=new NovaMasterDocument("AC-123","rev",new Dictionary<string,string?> {
    ["AC_NAME"]="Selected account",["AC_ADDR1"]="14 Liberty Drive",["AC_ADDR2"]=null,["AC_CITY"]="Londonderry",
    ["AC_STATE"]="NH",["AC_ZIP"]="03053",["AC_COUNTRY_CD"]="US",["AC_PHONE"]="5551234"
}.Select(p=>new NovaField(p.Key,p.Key,"text",100,false,p.Value)).ToArray());
var billTo=NovaAccountSelection.Values(selectedAccount,true);
Check(billTo["PB_BILL_ID"]=="AC-123" && billTo["PBBNAME"]=="Selected account" && billTo["PBBCOUNTRY"]=="US",
    "bill-to lookup maps account identity, name and country to Nova header");
Check(!billTo.ContainsKey("OPS_ACCTID") && !billTo.ContainsKey("PB_LOCATION"),"bill-to lookup cannot change stop or origin terminal");
var consignee=NovaAccountSelection.Values(selectedAccount,false);
Check(consignee["OPS_ACCTID"]=="AC-123" && consignee["OPS_NAME"]=="Selected account" && consignee["OPS_PHONE"]=="5551234",
    "shipper and consignee lookups map account identity and phone to the selected stop");
Check(consignee["OPS_ADDR2"] is null && consignee["OPS_CONTACT_EMAIL"] is null && consignee["OPS_LATITUDE"] is null,
    "selecting another account clears old optional contact and location values");
Check(!consignee.ContainsKey("OPS_APPT_LOW") && !consignee.ContainsKey("OPS_DELIVERY_TERMINAL"),"lookup preserves appointment and routing decisions");
Check(DataRequirements.Address("14 LIBERTY DRIVE", "LONDONDERRY", "03053").Length == 0, "MHT address does not require coordinates or country");
Check(DataRequirements.Address("14 Liberty Drive", null, "03053").Length == 0, "street and postal code accepted");
Check(DataRequirements.Address("14 Liberty Drive", "Londonderry", null).Length == 0, "street and city accepted");
Check(DataRequirements.Address(" ", "Londonderry", "03053").Length == 1, "missing street flagged");
Check(DataRequirements.Address("14 Liberty Drive", " ", null).Length == 1, "missing city and postal code flagged");
NovaMasterDocument Current(NovaMasterEdit edit) => new("MHT","rev",edit.Values.Select(p=>new NovaField(p.Key,p.Key,"text",50,false,p.Value)).ToArray());
var found = await lookup.Resolve("terminals",input,Current(input),default);
Check(found.Warning is null && found.Edit.Values["LM_LATITUDE"]=="42.9171", "missing coordinates looked up");
Check(input.Values["LM_LATITUDE"]=="0", "request not mutated");
var count=handler.Count;
await lookup.Resolve("terminals",found.Edit,Current(found.Edit),default);
Check(handler.Count==count,"unchanged valid location preserved");
var moved = found.Edit with { Values=new(found.Edit.Values) { ["LM_CITY"]="Portland" } };
handler.Body="{\"locations\":[]}";
var missing=await lookup.Resolve("terminals",moved,Current(found.Edit),default);
Check(missing.Warning is not null && missing.Edit.Values["LM_LATITUDE"] is null,"address change clears stale coordinates on no match");
var partial=input with { Values=new(input.Values) { ["LM_LATITUDE"]="42",["LM_LONGITUDE"]=null } };
Check((await lookup.Resolve("terminals",partial,null,default)).Edit.Values["LM_LATITUDE"] is null,"partial input cleared when lookup fails");
var noKey=new MasterAddressLookup(new HttpClient(handler),Options.Create(new PtvSettings()));
Check((await noKey.Resolve("terminals",input,null,default)).Warning!.Contains("not configured"),"missing configuration explained");
var manual=moved with { Values=new(moved.Values) { ["LM_LATITUDE"]="43",["LM_LONGITUDE"]="-70" } };
count=handler.Count;
var overridden=await lookup.Resolve("terminals",manual,Current(found.Edit),default);
Check(overridden.Warning is not null && handler.Count==count+1 && overridden.Edit.Values["LM_LATITUDE"] is null,"changed addresses cannot keep client-supplied coordinates");
handler.Body="{\"locations\":["+match+","+match.Replace("42.9171","43.1")+"]}";
Check((await lookup.Resolve("terminals",input,null,default)).Warning is not null,"ambiguous matches rejected");
handler.Body="{\"locations\":["+match.Replace("EXACT_ADDRESS","CITY")+"]}";
Check((await lookup.Resolve("terminals",input,null,default)).Warning is not null,"city centroid rejected");
handler.Body="{\"locations\":["+match.Replace("97","60")+"]}";
Check((await lookup.Resolve("terminals",input,null,default)).Warning is not null,"low confidence rejected");
handler.Status=HttpStatusCode.Forbidden;
Check((await lookup.Resolve("terminals",input,null,default)).Warning is not null,"provider failure allows save with warning");
handler.Timeout=true;
Check((await lookup.Resolve("terminals",input,null,default)).Warning is not null,"timeout allows save with warning");
using var cancelled=new CancellationTokenSource();cancelled.Cancel();
try { await lookup.Resolve("terminals",input,null,cancelled.Token); throw new Exception("cancellation swallowed"); } catch(OperationCanceledException) {}
handler.Timeout=false;handler.Status=HttpStatusCode.OK;handler.Body="{\"locations\":["+match+"]}";
var address = input.Values.ToDictionary(p=>p.Key.Replace("LM_","OPS_"),p=>p.Value);
var order = new NovaRecordEdit("rev",new() { ["header"]=new(),["pickup"]=new(address),["delivery"]=new(address) });
NovaRecordDocument OrderCurrent(NovaRecordEdit edit)=>new(1,"Order","rev",edit.Values.Select(p=>new NovaRecordPart(p.Key,p.Key,p.Value.Select(f=>new NovaField(f.Key,f.Key,"text",100,false,f.Value)).ToArray())).ToArray());
count=handler.Count;
var resolved=await lookup.ResolveOrder(order,null,default);
Check(handler.Count==count+2 && resolved.Warnings.Length==0,"new order resolves pickup and delivery");
Check(resolved.Edit.Values["delivery"]["OPS_LATITUDE"]=="42.9171" && order.Values["delivery"]["OPS_LATITUDE"]=="0","resolved point saved without mutating input");
count=handler.Count;
var existingMissing=await lookup.ResolveOrder(order,OrderCurrent(order),default);
Check(handler.Count==count+2 && existingMissing.Warnings.Length==0
    && existingMissing.Edit.Values["delivery"]["OPS_LONGITUDE"]=="-71.3629",
    "saving an existing order resolves missing coordinates even when its address is unchanged");
count=handler.Count;
await lookup.ResolveOrder(resolved.Edit,OrderCurrent(resolved.Edit),default);
Check(handler.Count==count,"unchanged order points skip provider");
var edited=resolved.Edit with { Values=resolved.Edit.Values.ToDictionary(p=>p.Key,p=>new Dictionary<string,string?>(p.Value)) };
edited.Values["delivery"]["OPS_ADDR2"]="Suite 2";
await lookup.ResolveOrder(edited,OrderCurrent(resolved.Edit),default);
Check(handler.Count==count+1,"only changed stop looked up including address line two");
handler.Body="{\"locations\":[]}";
var failed=await lookup.ResolveOrder(edited,OrderCurrent(resolved.Edit),default);
Check(failed.Warnings.Length==1 && failed.Edit.Values["delivery"]["OPS_LATITUDE"] is null && failed.Edit.Values["pickup"]["OPS_LATITUDE"]=="42.9171","failed moved stop clears stale point and preserves other stop");
var unconfigured=await noKey.ResolveOrder(order,null,default);
Check(unconfigured.Warnings.Length==2,"unconfigured order lookup reports both stops");
var tenantId=Guid.NewGuid();
var tenant=new LookupTenant(tenantId);
await using var dataSource=NpgsqlDataSource.Create("Host=localhost;Database=unused;Username=unused");
var source=new FirebirdRoutingSource(new NpgsqlConnectionFactory(dataSource,tenant),tenant,
    Options.Create(new FirebirdRoutingOptions { Enabled=true,TenantId=tenantId,SourceKey="check",ConnectionString="unused" }),lookup);
count=handler.Count;
await source.ResolveNodeForPlanning(new TerminalDto { Code="MHT",Latitude=42,Longitude=-71 },default);
var validPlan="""{"reporting":{"firebirdSource":"check"},"locations":[{"id":"DEPOT"},{"id":"FB-1","latitude":42,"longitude":-71}]}""";
await source.ResolveOrdersForPlanning(validPlan,default);
Check(handler.Count==count,"planning with stored coordinates needs neither Nova connection nor provider call");
var allOrders = """
{"reporting":{"firebirdSource":"check","orders":{"FB-1":{"nativeOrderId":1,"nativeStopId":10},"FB-1352073":{"nativeOrderId":1352073,"nativeStopId":20}}},
"locations":[{"id":"DEPOT","latitude":42,"longitude":-71},{"id":"FB-1","latitude":42,"longitude":-71},{"id":"FB-1352073"}],
"depots":[{"id":"DEPOT","locationId":"DEPOT"}],
"vehicles":[{"id":"TRUCK","start":{"locationId":"DEPOT"},"end":{"locationId":"DEPOT"}}],
"orders":{"deliveries":[{"id":"FB-1","delivery":{"locationId":"FB-1"}},{"id":"FB-1352073","delivery":{"locationId":"FB-1352073"}}]}}
""";
var selectedPlan = OrderWorkspace.SelectOrders(allOrders, ["FB-1"]);
var selectedRoot = RoutingInput.Parse(selectedPlan);
Check(RoutingInput.Items(selectedRoot["locations"]).Select(l=>RoutingInput.Text(l["id"])).SequenceEqual(["DEPOT","FB-1"]),
    "selection removes unselected locations while preserving the depot");
Check(selectedRoot["reporting"]!["orders"]!["FB-1"]!["nativeStopId"]!.GetValue<int>()==10,
    "selected order retains its verified Nova identity");
await source.ResolveOrdersForPlanning(selectedPlan,default);
source.PrepareDraft(selectedPlan,new TerminalDto { Code="MHT",Latitude=42,Longitude=-71 });
Check(handler.Count==count,"unselected order with missing coordinates is neither looked up nor validated");
Console.WriteLine("Address lookup checks passed.");

sealed class LookupHandler : HttpMessageHandler
{
    public string Body="";public int Count;public HttpStatusCode Status=HttpStatusCode.OK;public bool Timeout;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        Count++;
        if(Timeout)throw new TaskCanceledException();
        return Task.FromResult(new HttpResponseMessage(Status) { Content=new StringContent(Body) });
    }
}

sealed class LookupTenant(Guid id):ITenantContext { public Guid? TenantIdOrNull=>id; public Guid? UserIdOrNull=>null; }
