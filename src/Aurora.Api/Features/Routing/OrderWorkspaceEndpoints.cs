using Aurora.Integrations.Nova;
using Aurora.Contracts;
using Aurora.Modules.Routing;
using Aurora.Api.Features.Auth;

namespace Aurora.Api.Features.Routing;

public static class OrderWorkspaceEndpoints
{
    public static void MapOrderWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/aurora").RequireAuthorization().AddEndpointFilter<LocalWorkspaceFilter>();
        group.MapGet("/firebird/manifests/{id:long}", (long id,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>{
            var result=await source.ReadManifests(null,null,null,true,ct,manifestId:id);
            return result.Manifests.SingleOrDefault() is {} manifest ? Results.Ok(manifest) : Results.NotFound(new ApiErrorDto("Manifest no longer exists."));
        }));
        group.MapPost("/firebird/records/orders/lookup-address", (Dictionary<string,string?> input,MasterAddressLookup lookup,CancellationToken ct)=>Guard(async()=>Results.Ok(await lookup.LookUpOrderAddress(input,ct))));
        group.MapPost("/firebird/masters/terminals/lookup-address", (NovaMasterEdit input,MasterAddressLookup lookup,CancellationToken ct)=>Guard(async()=>Results.Ok(await lookup.LookUpTerminal(input,ct))));
        group.MapGet("/firebird/customers/{code}/planning",(string code,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadCustomerPlanning(code,ct))));
        group.MapPut("/firebird/customers/{code}/planning",(string code,CustomerPlanning input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveCustomerPlanning(code,input,ct))));
        group.MapPost("/firebird/manifests/{id:long}/release-consolidation",(long id,ConsolidationRelease input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReleaseConsolidation(id,input,ct))));
        group.MapGet("/firebird/drivers", (FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadDrivers(ct))));
        group.MapPut("/firebird/drivers/{id:long}", (long id,NovaDriver input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveDriver(id,input,ct))));
        group.MapDelete("/firebird/drivers/{id:long}", (long id,string revision,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>{await source.DeleteDriver(id,revision,ct);return Results.NoContent();}))
            .RequireAuthorization(p=>p.RequireRole("aurora:Admin","aurora:PowerUser"));
        group.MapGet("/firebird", (FirebirdRoutingSource source) => Results.Ok(source.Status));
        group.MapPut("/firebird/manifests/{id:long}/planning-lock", (long id,ManifestPlanningLockEdit input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SavePlanningLock(id,input,ct))));
        group.MapGet("/firebird/routing-configuration", (FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.RoutingConfiguration(ct))))
            .RequireAuthorization(p=>p.RequireRole("aurora:Admin"));
        group.MapPut("/firebird/routing-configuration", (NovaRoutingConfigurationEdit input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>{await source.SaveRoutingConfiguration(input,ct);return Results.Ok();}))
            .RequireAuthorization(p=>p.RequireRole("aurora:Admin"));
        group.MapGet("/firebird/terminals", (FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadTerminals(ct))));
        group.MapPut("/firebird/terminals/{code}/routing", (string code,TerminalDto input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveTerminalRouting(code,input,ct))));
        group.MapGet("/firebird/fleet", (FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadFleet(ct))));
        group.MapDelete("/firebird/records/fleet/{id:long}", (long id,string revision,HttpContext context,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>{var truck=await source.ReadRecord("fleet",id,ct);await source.DeleteTruck(id,revision,ct);context.Items[AppLog.ReferencesKey]=AppLog.BusinessReferences(context.Request.Path.Value!,truck);return Results.NoContent();}))
            .RequireAuthorization(p=>p.RequireRole("aurora:Admin","aurora:PowerUser"));
        group.MapDelete("/firebird/masters/terminals/{id}", (string id,string revision,HttpContext context,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>{await source.DeleteTransportationNode(id,revision,ct);context.Items[AppLog.ReferencesKey]="Terminal "+AppLog.SafeDetail(id);return Results.NoContent();}))
            .RequireAuthorization(p=>p.RequireRole("aurora:Admin","aurora:PowerUser"));
        group.MapGet("/firebird/masters/{kind}/new", (string kind,FirebirdRoutingSource source)=>Guard(()=>Task.FromResult<IResult>(Results.Ok(source.NewMaster(kind)))));
        group.MapPost("/firebird/masters/{kind}", (string kind,NovaMasterEdit input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.CreateMaster(kind,input,ct))));
        group.MapGet("/firebird/records/{kind}/new", (string kind,FirebirdRoutingSource source)=>Guard(()=>Task.FromResult<IResult>(Results.Ok(source.NewRecord(kind)))));
        group.MapPost("/firebird/records/{kind}", (string kind,NovaRecordEdit input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.CreateRecord(kind,input,ct))));
        group.MapGet("/firebird/masters/{kind}", (string kind,string? search,int? page,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadMasters(kind,search,page??0,ct))));
        group.MapGet("/firebird/masters/{kind}/{id}", (string kind,string id,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadMaster(kind,id,ct))));
        group.MapPut("/firebird/masters/{kind}/{id}", (string kind,string id,NovaMasterEdit input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveMaster(kind,id,input,ct))));
        group.MapGet("/firebird/equipment/{id:long}/routing-profile", (long id,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadEquipmentProfile(id,ct))));
        group.MapPut("/firebird/equipment/{id:long}/routing-profile", (long id,EquipmentRoutingProfileEdit input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveEquipmentProfile(id,input,ct))));
        group.MapGet("/firebird/record-options", (FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.RecordOptions(ct))));
        group.MapGet("/firebird/orders/{id:long}/routing", (long id,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadOrderRouting(id,ct))));
        group.MapPut("/firebird/orders/{id:long}/routing", (long id,NovaOrderRoutingEdit input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveOrderRouting(id,input,ct))));
        group.MapGet("/firebird/records/{kind}", (string kind,string? search,int? page,string? terminal,int? status,bool? readyOnly,bool? unknownTerminal,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.RecordList(kind,search,page??0,ct,terminal,status,readyOnly??false,unknownTerminal??false))));
        group.MapGet("/firebird/records/{kind}/{id:long}", (string kind,long id,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadRecord(kind,id,ct))));
        group.MapPut("/firebird/records/{kind}/{id:long}", (string kind,long id,NovaRecordEdit input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveRecord(kind,id,input,ct))));
        group.MapGet("/firebird/manifests", (DateOnly? from, DateOnly? to, int? status, bool? allDates, bool? manifestDates, FirebirdRoutingSource source, CancellationToken ct) => Guard(async () => Results.Ok(await source.ReadManifests(from,to,status,allDates ?? false,ct,manifestDates ?? false))));
        group.MapPost("/firebird/manifests", (NovaManifestCreate input,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.CreateManifest(input,ct))));
        group.MapPut("/firebird/manifests/{id:long}/notes", (long id,NovaManifestNotesEdit input,HttpContext context,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveManifestNotes(id,input,context.User.Identity?.Name ?? "Aurora",ct))));
        group.MapGet("/firebird/manifests/{id:long}/dispatch", (long id,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.ReadDispatch(id,ct))));
        group.MapPut("/firebird/manifests/{id:long}/assignment", (long id,NovaDispatchEdit input,HttpContext context,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveDispatch(id,input,false,context.User.Identity?.Name ?? "Aurora",ct))));
        group.MapPost("/firebird/manifests/{id:long}/dispatch", (long id,NovaDispatchEdit input,HttpContext context,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>Results.Ok(await source.SaveDispatch(id,input,true,context.User.Identity?.Name ?? "Aurora",ct))));
        group.MapPut("/firebird/manifests/{id:long}/status", (long id,NovaManifestStatusEdit input,HttpContext context,FirebirdRoutingSource source,CancellationToken ct)=>Guard(async()=>
        {
            var result=await source.SaveManifestStatus(id,input,context.User.Identity?.Name ?? "Aurora",ct);
            context.Items[AppLog.DetailKey]=$"Manifest status: {result.Status}";
            context.Items[AppLog.ActionKey]=result.StatusCode switch { "ASG"=>"Assigned manifest", "DPCH"=>"Dispatched manifest", "ENR"=>"Departed manifest", "CPLT"=>"Closed manifest", _=>"Changed manifest status" };
            return Results.Ok(result);
        }));
        group.MapGet("/firebird/manifests/{id:long}/orders", (long id, FirebirdRoutingSource source, CancellationToken ct) => Guard(async () => Results.Ok(await source.ReadManifestOrders(id,ct))));
        group.MapGet("/firebird/manifests/{id:long}/orders/{orderId:long}", (long id, long orderId, FirebirdRoutingSource source, CancellationToken ct) => Guard(async () => Results.Ok(await source.ReadOrderDetails(id,orderId,ct))));
        group.MapPost("/firebird/refresh", (FirebirdRoutingSource source, CancellationToken ct) => Guard(async () => Results.Ok(await source.Synchronize(ct))));
        group.MapGet("/sources", (OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Sources(ct))));
        group.MapGet("/orders", (Guid source,DateTimeOffset? from,DateTimeOffset? to,string? status,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Orders(source,from,to,status,ct))));
        group.MapPost("/drafts", (CreateOrderDraftDto input,HttpContext context,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.CreateDraft(input,PlanningSessions.Owner(context.User),ct))));
        group.MapPost("/plans", (CreateOrderDraftDto input,HttpContext context,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.CreatePlans(input,PlanningSessions.Owner(context.User),ct))));
        group.MapGet("/drafts", (HttpContext context,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.OpenDrafts(PlanningSessions.Owner(context.User),ct))));
        group.MapGet("/drafts/{id:guid}", (Guid id,HttpContext context,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Draft(id,PlanningSessions.Owner(context.User),ct))));
        group.MapGet("/manifests", (OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Manifests(ct))));
        group.MapGet("/manifests/{id:guid}", (Guid id,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Manifest(id,ct))));
        group.MapPut("/manifests/{id:guid}", (Guid id,UpdateManifestDto input,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.UpdateManifest(id,input,ct))));
        group.MapPost("/drafts/{id:guid}/cancel", (Guid id,HttpContext context,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>{ await store.CancelDraft(id,PlanningSessions.Owner(context.User),ct); return Results.Ok(); }));
        group.MapPost("/drafts/{id:guid}/finish", (Guid id,FinishOrderDraftDto input,HttpContext context,OrderWorkspace store,PlanningSessions sessions,CancellationToken ct)=>Guard(async()=>
        {
            var result=await sessions.Completed(input.SessionId,id,context.User,ct);
            return Results.Ok(await store.Finish(id,input.SessionId,PlanningSessions.Owner(context.User),result,ct,input.Vehicles,await sessions.CompletedRequest(input.SessionId,id,context.User,ct)));
        }));
    }
    internal static async Task<IResult> Guard(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch(FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message) { RecordLink=(ex as RecordErrorException)?.Link }); }
        catch(KeyNotFoundException ex) { return Results.NotFound(new ApiErrorDto(ex.Message)); }
        catch(TimeoutException) { return Results.Json(new ApiErrorDto("The database request timed out. Check the Nova server address, port, and network connection in Admin > Users."),statusCode:503); }
        catch(FirebirdSql.Data.FirebirdClient.FbException) { return Results.Json(new ApiErrorDto("The Nova database request failed. No success is assumed; retry the same operation to recover a completed export."), statusCode:503); }
        catch(NotSupportedException ex) when(ex.Message.StartsWith("Not supported plugin '",StringComparison.Ordinal)) { return Results.Json(new ApiErrorDto(NovaConnectionErrors.UnsupportedPlugin(ex)),statusCode:503); }
    }
}
