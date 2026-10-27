using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Aurora.Modules.Routing.Optimization;
using Dapper;
using System.Text.Json.Nodes;

static class LocalPlanningChecks
{
    public static async Task Run(NpgsqlConnectionFactory factory,ITenantContext tenant,Action<bool,string> check)
    {
        var terminals=new TerminalStore(factory,tenant);var equipment=new EquipmentStore(factory,tenant);var drivers=new DriverStore(factory,tenant);
        var source=new LocalPlanningDataSource(factory,tenant,terminals,equipment,drivers);
        var tms=new TmsStore(factory,tenant,source);var workspace=new OrderWorkspace(factory,tenant,source);
        var terminal=new TerminalDto{Code="PLN",Name="Local planning",Latitude=40,Longitude=-80};
        await terminals.Save(terminal,true,default);
        await equipment.Save(new EquipmentTypeDto{Code="PLN",Description="Box truck",Weight=10000,Cubes=1000,PerHour=25,PerKilometer=1},default);
        await equipment.Save(new EquipmentUnitDto{Id="PLN-TRUCK",TypeCode="PLN",Terminal="PLN",Available=true,UseForRouting=true,UtcOffset="-04:00"},default);
        var originalFleet=await equipment.List(default);
        var savedType=originalFleet.Types.Single(x=>x.Code=="PLN");
        savedType.Description="Updated box truck";await equipment.Save(savedType,default);
        check(savedType.Revision==2,"equipment save advances its optimistic revision");
        try{await equipment.Save(new EquipmentTypeDto{Code="PLN",Description="Stale overwrite",Revision=1},default);throw new Exception("Stale equipment accepted");}
        catch(FormatException){check(true,"stale equipment edits are rejected");}
        var savedUnit=(await equipment.List(default)).Units.Single(x=>x.Id=="PLN-TRUCK");
        savedUnit.Alias="Updated truck";await equipment.Save(savedUnit,default);
        try{await equipment.Save(new EquipmentUnitDto{Id="PLN-TRUCK",TypeCode="PLN",Terminal="PLN",Revision=1},default);throw new Exception("Stale unit accepted");}
        catch(FormatException){check(true,"stale unit edits are rejected");}
        check((await equipment.List(default)).Units.Single(x=>x.Id=="PLN-TRUCK").Alias=="Updated truck","rejected edit preserves saved equipment");
        var driver=await drivers.Save(0,new(){Number="PLN-DRIVER",LastName="Planner",Terminal="PLN",PerHour=20},default);
        TmsOrder Order(string id)=>new(){Id=id,Customer="Local consignee",TerminalId=terminal.Id,UseConsigneeAddress=true,Latitude=40.1,Longitude=-80.1,Weight=100,Cube=10,Pieces=1};
        var first=Order("LOCAL-1");await tms.SaveOrder(first,true,default);
        var day=new DateOnly(2040,6,1);
        async Task<(OrderDraftDto Draft,string Request)> Plan(string id)
        {
            var draft=await workspace.CreateDraft(new(first.SourceId,[id],day),"local-test",default);
            var request=RoutingInput.Parse(draft.RequestJson);
            request["reporting"]!["drivers"]=new JsonObject{["PLN-TRUCK"]=new JsonObject{["id"]=driver.Id.ToString(),["shiftStart"]="06:00",["shiftEnd"]="18:00"}};
            return(draft,await workspace.ValidateDraft(draft.Id,"local-test",request.ToJsonString(),default));
        }
        OptimizationResultDto Result(string request)
        {
            var root=RoutingInput.Parse(request);var stops=new JsonArray();
            foreach(var order in RoutingInput.Orders(root))
                stops.Add(new JsonObject{["locationId"]=RoutingInput.Text(RoutingInput.CustomerTask(order)?["locationId"]),["appointments"]=new JsonArray(new JsonObject{["tasks"]=new JsonArray(new JsonObject{["orderId"]=RoutingInput.Text(order["id"]),["type"]="DELIVERY"})})});
            var raw=new JsonObject{["status"]="SUCCEEDED",["metrics"]=new JsonObject{["numberOfScheduledOrders"]=stops.Count,["numberOfUnscheduledOrders"]=0},["routes"]=new JsonArray(new JsonObject{["vehicleId"]="PLN-TRUCK",["stops"]=stops})};
            return OptimizationService.Describe("local.json",request,new PtvRun("fixture","SUCCEEDED",new PtvResponse(200,"OK",raw.ToJsonString()),TimeSpan.FromSeconds(1)));
        }
        var plan=await Plan(first.Id);
        check(RoutingInput.Text(RoutingInput.Parse(plan.Request)["reporting"]?["firebirdSource"]) is null,"local plan has no Nova source dependency");
        check(RoutingInput.Parse(plan.Request)["vehicles"]![0]!["costs"]!["perHour"]!.GetValue<double>()==45,"local planning combines saved equipment and driver costs");
        var session=Guid.NewGuid();
        var manifests=await workspace.Finish(plan.Draft.Id,session,"local-test",Result(plan.Request),default,acceptedRequest:plan.Request);
        check(manifests.Count==1&&manifests[0].Details?.DriverId==driver.Id,"local optimization creates a manifest with its driver");
        check((await workspace.Finish(plan.Draft.Id,session,"local-test",Result(plan.Request),default,acceptedRequest:plan.Request)).Single().Id==manifests[0].Id,"local Create Manifest retry returns the existing manifest");
        await using var db=await factory.OpenConnectionAsync();
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='manifest'")==1,"Create Manifest queues one external update in the same commit");
        var editedDetails=manifests[0].Details!;editedDetails.Notes="Local manual edit";
        var editedManifest=await workspace.UpdateManifest(manifests[0].Id,new(editedDetails,manifests[0].Revision),default);
        check(editedManifest.Details!.Notes=="Local manual edit"&&editedManifest.Revision==manifests[0].Revision+1,"manual manifest edit persists with a new revision");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='manifest'")==2,"manual manifest edit queues its external update atomically");
        try{await workspace.UpdateManifest(manifests[0].Id,new(editedDetails,manifests[0].Revision),default);throw new Exception("Stale manifest edit accepted");}
        catch(FormatException){check(true,"stale manual manifest edit is rejected");}
        try{await tms.SaveManifest(new TmsManifest{Number="CONFLICT",Vehicle="PLN-TRUCK",ManifestDate=day,Details=new(){DriverId=driver.Id,OriginTerminal="PLN"}},true,default);throw new Exception("Double booking accepted");}
        catch(FormatException ex)when(ex.Message.Contains("reserved")){check(true,"manual manifest cannot reserve an already booked truck or driver");}
        check((await tms.Manifests(default)).Count==1,"rejected manual assignment creates no manifest");
        var second=Order("LOCAL-2");await tms.SaveOrder(second,true,default);
        var replan=await Plan(second.Id);
        check(ManifestReplanning.Included(replan.Request).Single().LocalId==manifests[0].Id,"local reoptimization includes the existing unlocked manifest");
        var replanned=await workspace.Finish(replan.Draft.Id,Guid.NewGuid(),"local-test",Result(replan.Request),default,acceptedRequest:replan.Request);
        check(replanned.Count==1&&replanned[0].Route.Pros.Count==2,"local reoptimization preserves existing orders while adding new work");
        check((await tms.Manifests(default)).Count==1,"replaced manifest is removed from active local manifests");
        var complete=(await tms.Manifests(default)).Single();complete.Status="Complete";complete.Details.StatusOccurredAt=DateTime.Now;await tms.SaveManifest(complete,false,default);
        var third=Order("LOCAL-3");await tms.SaveOrder(third,true,default);
        var stale=await Plan(third.Id);
        var changed=(await tms.OrdersById(third.SourceId,[third.Id],default)).Single();changed.Weight=200;await tms.SaveOrder(changed,false,default);
        try{await workspace.Finish(stale.Draft.Id,Guid.NewGuid(),"local-test",Result(stale.Request),default,acceptedRequest:stale.Request);throw new Exception("Stale order accepted");}
        catch(FormatException ex)when(ex.Message.Contains("order changed",StringComparison.OrdinalIgnoreCase)){check(true,"local manifest creation rejects an order changed since planning");}
        check((await tms.Manifests(default)).Count==1,"rejected stale result does not create a partial manifest");
        await workspace.CancelDraft(stale.Draft.Id,"local-test",default);
    }
}
