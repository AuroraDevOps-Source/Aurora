using Aurora.Integrations.Nova;
using Aurora.Contracts;
using Aurora.Modules.Routing;
using Npgsql;
namespace Aurora.Api.Features.Routing;
public static class TmsEndpoints
{
 public static void MapTmsEndpoints(this IEndpointRouteBuilder app) {
  var g=app.MapGroup("/api/v1/tms").RequireAuthorization();
  g.MapPost("/orders/lookup-address",(Dictionary<string,string?> input,MasterAddressLookup lookup,CancellationToken ct)=>Guard(async()=>Results.Ok(await lookup.LookUpOrderAddress(input,ct))));
  g.MapGet("/drivers",(DriverStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.List(ct))));
  g.MapPost("/drivers",(NovaDriver input,DriverStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.Save(0,input,ct))));
  g.MapPut("/drivers/{id:long}",(long id,NovaDriver input,DriverStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.Save(id,input,ct))));
  g.MapDelete("/drivers/{id:long}",(long id,string revision,DriverStore s,CancellationToken ct)=>Guard(async()=>{await s.Delete(id,revision,ct);return Results.Ok();}));
  g.MapGet("/orders/{source:guid}/{id}/workflow",(Guid source,string id,FreightWorkflowStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.Get(source,id,ct))));
  g.MapPut("/orders/{source:guid}/{id}/workflow",(Guid source,string id,FreightWorkflow input,FreightWorkflowStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.Save(source,id,input,ct))));
  g.MapPost("/orders/{source:guid}/{id}/workflow/events",(Guid source,string id,FreightExecution input,FreightWorkflowStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.Execute(source,id,input,ct))));
  g.MapGet("/customers",(CustomerStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.List(ct))));
  g.MapPost("/customers",(CustomerDto input,CustomerStore s,CancellationToken ct)=>Guard(async()=>{await s.Save(input,true,ct);return Results.Ok(input);}));
  g.MapPut("/customers",(CustomerDto input,CustomerStore s,CancellationToken ct)=>Guard(async()=>{await s.Save(input,false,ct);return Results.Ok();}));
  g.MapDelete("/customers/{id:guid}",(Guid id,int revision,CustomerStore s,CancellationToken ct)=>Guard(async()=>{await s.Delete(id,revision,ct);return Results.NoContent();})).RequireAuthorization(p=>p.RequireRole("aurora:Admin","aurora:PowerUser"));
  g.MapPost("/terminals",(TerminalDto input,TerminalStore s,CancellationToken ct)=>Guard(async()=>{await s.Save(input,true,ct);return Results.Ok(input);}));
  g.MapPut("/terminals",(TerminalDto input,TerminalStore s,CancellationToken ct)=>Guard(async()=>{await s.Save(input,false,ct);return Results.Ok();}));
  g.MapDelete("/terminals/{id:guid}",(Guid id,int revision,TerminalStore s,CancellationToken ct)=>Guard(async()=>{await s.Delete(id,revision,ct);return Results.NoContent();})).RequireAuthorization(p=>p.RequireRole("aurora:Admin","aurora:PowerUser"));
  g.MapGet("/terminals",(IPlanningDataSource s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.ReadTerminals(ct))));
  g.MapGet("/orders",(DateTimeOffset? from,DateTimeOffset? to,string? status,string? search,Guid? manifest,Guid? terminal,TmsStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.Orders(from,to,status,search,manifest,ct,terminal))));
  g.MapPost("/orders",(TmsOrder item,TmsStore s,CancellationToken ct)=>Guard(async()=>{await s.SaveOrder(item,true,ct);return Results.Ok(item);}));
  g.MapPut("/orders",(TmsOrder item,TmsStore s,CancellationToken ct)=>Guard(async()=>{await s.SaveOrder(item,false,ct);return Results.Ok();}));
  g.MapDelete("/orders/{source:guid}/{id}",(Guid source,string id,int revision,TmsStore s,CancellationToken ct)=>Guard(async()=>{await s.DeleteOrder(source,id,revision,ct);return Results.Ok();}));
  g.MapGet("/manifests",(TmsStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.Manifests(ct))));
  g.MapPost("/manifests",(TmsManifest item,TmsStore s,CancellationToken ct)=>Guard(async()=>{await s.SaveManifest(item,true,ct);return Results.Ok(item);}));
  g.MapPut("/manifests",(TmsManifest item,TmsStore s,CancellationToken ct)=>Guard(async()=>{await s.SaveManifest(item,false,ct);return Results.Ok();}));
  g.MapPost("/manifests/{id:guid}/orders",(Guid id,TmsAssignmentChange input,TmsStore s,CancellationToken ct)=>Guard(async()=>{await s.ChangeOrders(id,input,false,false,ct);return Results.Ok();}));
  g.MapPost("/manifests/{id:guid}/remove-orders",(Guid id,TmsAssignmentChange input,TmsStore s,CancellationToken ct)=>Guard(async()=>{await s.ChangeOrders(id,input,true,false,ct);return Results.Ok();}));
  g.MapDelete("/manifests/{id:guid}",(Guid id,int revision,TmsStore s,CancellationToken ct)=>Guard(async()=>{await s.ChangeOrders(id,new([],revision),true,true,ct);return Results.Ok();}));
 }
 static async Task<IResult> Guard(Func<Task<IResult>> action) {
  try{return await action();}
  catch(FreightConflictException e){return Results.Conflict(new ApiErrorDto(e.Message));}
  catch(FormatException e){return Results.BadRequest(new ApiErrorDto(e.Message));}
  catch(KeyNotFoundException e){return Results.NotFound(new ApiErrorDto(e.Message));}
  catch(PostgresException e) when(e.SqlState=="23505"){return Results.Conflict(new ApiErrorDto("That code or number already exists (including deleted records), or the order is already assigned."));}
 }
}
