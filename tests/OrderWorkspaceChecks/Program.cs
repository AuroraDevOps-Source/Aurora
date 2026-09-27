using Aurora.Contracts;
using Aurora.Modules.Routing;
using Aurora.Modules.Routing.Optimization;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Npgsql;
using Dapper;
using System.Text.Json.Nodes;

var count=0;
void Check(bool value,string name) { if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);count++; }
void Reject(Action action,string name) { try { action(); } catch(FormatException) { Check(true,name);return; }throw new Exception(name); }
var json=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"sample.json"));
var root=RoutingInput.Parse(json);
var ids=OrderWorkspace.OrderIds(json);
var selected=OrderWorkspace.SelectOrders(json,[ids[0],ids[1]]);
Check(OrderWorkspace.OrderIds(selected).Length==2,"only selected orders enter request");
Check(OrderWorkspace.OrderIds(json).Length==3,"original input unchanged");
Check(RoutingInput.Parse(selected)["vehicles"]!.ToJsonString()==root["vehicles"]!.ToJsonString(),"fleet constraints preserved");
Reject(()=>OrderWorkspace.SelectOrders(json,["unknown"]),"unknown orders rejected");
Reject(()=>OrderWorkspace.SelectOrders(json,[ids[0],ids[0]]),"duplicate selection rejected");
Reject(()=>OrderWorkspace.SelectOrders(json,[]),"empty selection rejected");
var location=RoutingInput.Text(root["orders"]!["deliveries"]![0]!["delivery"]!["locationId"])!;
var truck=RoutingInput.Text(root["vehicles"]![0]!["id"])!;
var raw=new JsonObject { ["status"]="SUCCEEDED",["metrics"]=new JsonObject { ["numberOfScheduledOrders"]=1,["numberOfUnscheduledOrders"]=2 },["routes"]=new JsonArray(new JsonObject {
 ["vehicleId"]=truck,["stops"]=new JsonArray(new JsonObject { ["locationId"]=location,["appointments"]=new JsonArray(new JsonObject { ["tasks"]=new JsonArray(new JsonObject { ["orderId"]=ids[0],["type"]="DELIVERY" }) }) }) }) };
var result=OptimizationService.Describe("sample.json",json,new PtvRun("test","SUCCEEDED",new PtvResponse(200,"OK",raw.ToJsonString()),TimeSpan.FromSeconds(1)));
Check(OrderWorkspace.Assignments(result,ids).Single().Orders.Single()==ids[0],"saved assignment uses parsed provider stop order");
Reject(()=>OrderWorkspace.Assignments(result,[ids[1]]),"unknown result order rejected");
Reject(()=>OrderWorkspace.Assignments(result with { Routes=result.Routes.Concat(result.Routes).ToArray() },ids),"duplicate result assignment rejected");
Reject(()=>OrderWorkspace.Assignments(result with { Status="RUNNING" },ids),"interim result cannot be finished");
var terminal=RoutingInput.Text(root["reporting"]?["terminal"])!;
var type=new EquipmentTypeDto {Code="CHECK",Description="Saved truck",Weight=12345,PerHour=42};
var unit=new EquipmentUnitDto {Id=truck,TypeCode=type.Code,Terminal=terminal,UseForRouting=true,ShiftStart="20:00",ShiftEnd="06:00"};
var fleet=new EquipmentCatalogDto([type],[unit]);
var fleetInput=FleetPlanning.Build(selected,fleet);
Check(RoutingInput.Items(RoutingInput.Parse(fleetInput)["vehicles"]).Count()==1,"fleet comes from saved truck records, not file vehicles");
var built=RoutingInput.Parse(fleetInput)["vehicles"]![0]!;
Check(RoutingInput.Time(built["end"]!["latestEndTime"])>RoutingInput.Time(built["start"]!["earliestStartTime"]),"overnight shift advances return date");
var edited=RoutingInput.Parse(fleetInput);edited["vehicles"]![0]!["costs"]!["perHour"]=0;
var canonical=RoutingInput.Parse(FleetPlanning.ApplySavedFleet(edited.ToJsonString(),fleet,terminal));
Check(canonical["vehicles"]![0]!["costs"]!["perHour"]!.GetValue<double>()==42,"server restores saved fleet costs");
Reject(()=>FleetPlanning.Build(json,new([type],[])),"empty fleet does not fall back to file trucks");
Reject(()=>FleetPlanning.ApplySavedFleet(fleetInput,fleet,"OTHER"),"another terminal's trucks are rejected");
unit.Available=false;
Reject(()=>FleetPlanning.ApplySavedFleet(fleetInput,fleet,terminal),"unavailable saved truck cannot optimize");
unit.Available=true;
Check(WorkspaceOrderStatus.Label(WorkspaceOrderStatus.Ready)=="Ready to Ship" && WorkspaceOrderStatus.All.Contains("AwaitingAppointment"),"Nova lifecycle labels include requested ready state");
var connection=Environment.GetEnvironmentVariable("WORKSPACE_CHECK_CONNECTION");
if(connection is not null)
{
 var settings=new NpgsqlConnectionStringBuilder(connection);
 if(!settings.Database!.StartsWith("aurora_workspace_checks_"))throw new Exception("Integration checks require an isolated checks database.");
 await using var source=NpgsqlDataSource.Create(connection);
 var tenant=new TestTenant(Guid.NewGuid());
 var factory=new NpgsqlConnectionFactory(source,tenant);
 await using(var db=await factory.OpenConnectionAsync()) await db.ExecuteAsync("INSERT INTO tenant(id,name,slug) VALUES (@Id,'Workspace checks',@Slug)",new { Id=tenant.TenantIdOrNull,Slug=Guid.NewGuid().ToString("N") });
 var store=new OrderWorkspace(factory,tenant);
 await store.Import(new("Fixture",json),default);
 await store.Import(new("Fixture",json),default);
 var import=(await store.Sources(default)).Single();
 var orders=(await store.Orders(import.Id,null,null,null,default)).ToArray();
 Check(orders.Length==3,"import idempotency and order persistence");
 Check((await store.Orders(import.Id,orders[0].ScheduledAt,orders[0].ScheduledAt,null,default)).Any(),"inclusive datetime filter");
 Check(!(await store.Orders(import.Id,null,null,"Routed",default)).Any(),"initial status is available");
 var equipment=new EquipmentStore(factory,tenant);
 await equipment.AddMissing(fleet,default);
 Check((await equipment.List(default)).Units.Single().UseForRouting,"planning eligibility persists in tenant equipment table");
 var draft=await store.CreateDraft(new(import.Id,ids),"owner",default);
 Check((await store.Draft(draft.Id,"owner",default)).Id==draft.Id,"draft recovery");
 try { await store.Draft(draft.Id,"other",default);throw new Exception("owner isolation"); }catch(KeyNotFoundException){Check(true,"draft owner isolation");}
 var otherTenant=new TestTenant(Guid.NewGuid());
 var otherFactory=new NpgsqlConnectionFactory(source,otherTenant);
 var other=new OrderWorkspace(otherFactory,otherTenant);
 await using(var hidden=await otherFactory.OpenConnectionAsync()) Check(await hidden.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_order")==0,"database RLS hides orders without a tenant WHERE clause");
 Check(!(await other.Sources(default)).Any(),"cross tenant data hidden");
 Check(!(await new EquipmentStore(otherFactory,otherTenant).List(default)).Units.Any(),"another tenant cannot read saved trucks");
 var tenantFleetDraft=RoutingInput.Parse(draft.RequestJson);
 Check(tenantFleetDraft["vehicles"]!.AsArray().Count==1,"database draft contains only eligible tenant fleet");
 var conflicting=await store.CreateDraft(new(import.Id,ids),"owner",default);
 var session=Guid.NewGuid();
 var manifests=await store.Finish(draft.Id,session,"owner",result,default);
 Check(manifests.Count==1 && manifests[0].Route.Pros.Single().ProNumber==ids[0],"one persisted manifest per route");
 Check((await store.Orders(import.Id,null,null,"ReadyToRoute",default)).Count()==2,"unscheduled orders remain available");
 Check((await store.Finish(draft.Id,session,"owner",result,default)).Single().Id==manifests[0].Id,"finish retry returns same manifest");
 try {await store.Finish(conflicting.Id,Guid.NewGuid(),"owner",result,default);throw new Exception("conflict missing");}catch(FormatException){Check(true,"overlapping plan cannot duplicate assignment");}
 Check((await store.Manifests(default)).Count==1,"conflicting finish is atomic");
 var details=new ManifestDetailsDto {BillToCode="ACCT-100",BillToName="Test Bill To",Carrier="Test Carrier",Driver="Test Driver",Notes="Handle carefully"};
 var editedManifest=await store.UpdateManifest(manifests[0].Id,new(details,0),default);
 Check(editedManifest.Details?.BillToCode=="ACCT-100" && editedManifest.Revision==1,"manifest details persist with revision");
 Check(System.Text.Json.JsonSerializer.Serialize(editedManifest.Route)==System.Text.Json.JsonSerializer.Serialize(manifests[0].Route),"editing details preserves optimized route snapshot");
 Check((await store.Manifests(default)).Single().Details?.Carrier=="Test Carrier","details survive reload");
 try {await store.UpdateManifest(editedManifest.Id,new(details,0),default);throw new Exception("stale update accepted");}catch(FormatException){Check(true,"stale manifest edit rejected");}
 try {await other.UpdateManifest(editedManifest.Id,new(details,1),default);throw new Exception("cross tenant edit accepted");}catch(KeyNotFoundException){Check(true,"cross tenant manifest edit rejected");}
 try {await store.UpdateManifest(editedManifest.Id,new(new ManifestDetailsDto {Notes=new string('x',4001)},1),default);throw new Exception("invalid details accepted");}catch(FormatException){Check(true,"oversized notes rejected");}


 var tms=new TmsStore(factory,tenant); var hiddenTms=new TmsStore(otherFactory,otherTenant);
 async Task Fails(Func<Task> action,string name){try{await action();}catch(Exception ex) when(ex is FormatException or KeyNotFoundException or PostgresException){Check(true,name);return;}throw new Exception(name);}
 var shipment=new TmsOrder{Id="CRUD-100",Customer="Acme",City="Philadelphia",Address="123 Test St",Address2="Dock 2",Country="US",Shipper="Origin Co",OriginAddress="456 Origin Ave",OriginAddress2="Suite 9",OriginCity="Boston",OriginState="MA",OriginPostalCode="02101",OriginCountry="US",BillTo="Billing Co",BillToAddress="789 Accounts Rd",BillToAddress2="Floor 3",BillToCity="Toronto",BillToState="ON",BillToPostalCode="M5V 2T6",BillToCountry="CA",Pieces=5,Weight=900,ScheduledAt=DateTimeOffset.UtcNow};
 await tms.SaveOrder(shipment,true,default);
 var loaded=(await tms.Orders(null,null,null,"CRUD-100",null,default)).Single();
 Check(loaded.Address==shipment.Address && loaded.Weight==900,"manual shipment fields persist");
 Check(loaded.BillToAddress=="789 Accounts Rd" && loaded.BillToAddress2=="Floor 3" && loaded.BillToCity=="Toronto" && loaded.BillToState=="ON" && loaded.BillToPostalCode=="M5V 2T6" && loaded.BillToCountry=="CA" && loaded.OriginAddress=="456 Origin Ave" && loaded.OriginAddress2=="Suite 9" && loaded.OriginCountry=="US" && loaded.Country=="US" && loaded.Address2=="Dock 2","all three independent addresses survive database reload");
 var copied=System.Text.Json.JsonSerializer.Deserialize<TmsOrder>(System.Text.Json.JsonSerializer.Serialize(loaded))!;
 copied.CopyBillTo(true);Check(copied.BillTo=="Origin Co" && copied.BillToAddress=="456 Origin Ave" && copied.BillToAddress2=="Suite 9" && copied.BillToPostalCode=="02101","copy shipper includes complete billing address");
 copied.CopyBillTo(false);Check(copied.BillTo=="Acme" && copied.BillToAddress=="123 Test St" && copied.BillToAddress2=="Dock 2" && copied.BillToCountry=="US","copy consignee includes complete billing address");
 copied.BillToAddress="Independent edit";Check(copied.Address=="123 Test St" && copied.OriginAddress=="456 Origin Ave","billing copy stays independently editable");
 loaded.Customer="Acme edited";await tms.SaveOrder(loaded,false,default);
 await Fails(()=>tms.SaveOrder(loaded,false,default),"stale order edit rejected");
 Check(!(await hiddenTms.Orders(null,null,null,null,null,default)).Any(),"TMS order tenant isolation");
 Check(!(await tms.Orders(DateTimeOffset.UtcNow.AddDays(1),null,null,"CRUD-100",null,default)).Any(),"TMS order date filtering");
 await Fails(()=>hiddenTms.DeleteOrder(loaded.SourceId,loaded.Id,1,default),"cross tenant order delete rejected");
 var manifest=new TmsManifest{Number="TEST-100",Vehicle=truck,Details=new(){Carrier="Carrier",BillToName="Bill to"}};
 await tms.SaveManifest(manifest,true,default);
 var fresh=(await tms.Manifests(default)).Single(m=>m.Id==manifest.Id);
 Check(fresh.Details.Carrier=="Carrier" && fresh.OrderCount==0,"manual manifest created without optimization");
 var key=new TmsOrderKey(loaded.SourceId,loaded.Id);
 await tms.ChangeOrders(fresh.Id,new([key],0),false,false,default);
 loaded=(await tms.Orders(null,null,null,null,fresh.Id,default)).Single();
 Check(loaded.Status=="Routed" && loaded.ManifestNumber=="TEST-100","assignment persists and marks order Routed");
 await Fails(()=>tms.DeleteOrder(loaded.SourceId,loaded.Id,loaded.Revision,default),"assigned order cannot be deleted");
 await Fails(()=>tms.ChangeOrders(fresh.Id,new([key],0),true,false,default),"stale assignment change rejected");
 var second=new TmsManifest{Number="TEST-200"};await tms.SaveManifest(second,true,default);
 await Fails(()=>tms.ChangeOrders(second.Id,new([key],0),false,false,default),"order cannot be assigned twice");
 await Fails(()=>hiddenTms.ChangeOrders(fresh.Id,new([key],1),true,false,default),"cross tenant membership rejected");
 await tms.ChangeOrders(fresh.Id,new([key],1),true,false,default);
 loaded=(await tms.Orders(null,null,null,"CRUD-100",null,default)).Single();
 Check(loaded.Status==WorkspaceOrderStatus.Ready && loaded.ManifestId is null,"removing order returns it to Ready to Ship");
 await tms.ChangeOrders(fresh.Id,new([key],2),false,false,default);
 await tms.ChangeOrders(fresh.Id,new([],3),true,true,default);
 Check(!(await tms.Manifests(default)).Any(m=>m.Id==fresh.Id),"deleted manifest disappears");
 loaded=(await tms.Orders(null,null,null,"CRUD-100",null,default)).Single();
 Check(loaded.ManifestId is null && loaded.Status==WorkspaceOrderStatus.Ready,"manifest deletion releases orders");
 await tms.ChangeOrders(second.Id,new([key],0),false,false,default);
 var dispatch=(await tms.Manifests(default)).Single(m=>m.Id==second.Id);dispatch.Status="Dispatched";await tms.SaveManifest(dispatch,false,default);
 loaded=(await tms.Orders(null,null,null,"CRUD-100",null,default)).Single();Check(loaded.Status=="Dispatched","manifest lifecycle updates order status");
 await Fails(()=>tms.ChangeOrders(second.Id,new([],2),true,true,default),"dispatched manifest cannot be deleted");
 await Fails(()=>tms.ChangeOrders(second.Id,new([key],2),true,false,default),"dispatched assignments locked");
 await Fails(()=>tms.SaveOrder(loaded,false,default),"dispatched order edit locked");
 var disposable=new TmsOrder{Id="DELETE-ME",Customer="Temp"};await tms.SaveOrder(disposable,true,default);await tms.DeleteOrder(disposable.SourceId,disposable.Id,0,default);
 Check(!(await tms.Orders(null,null,null,"DELETE-ME",null,default)).Any(),"unassigned order delete persists");
 if(args.Length>0) { await store.Import(new("Desktop appointment file",File.ReadAllText(args[0])),default); var desktop=(await store.Sources(default)).Single(s=>s.Name=="Desktop appointment file"); Check((await store.Orders(desktop.Id,null,null,null,default)).Count()==100,"all 100 desktop orders import with dates"); }
}
Console.WriteLine($"{count} order workspace checks passed.");
sealed record TestTenant(Guid Id) : ITenantContext { public Guid? TenantIdOrNull=>Id;public Guid? UserIdOrNull=>Guid.Empty; }

