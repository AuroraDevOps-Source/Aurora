using Aurora.Contracts;
using Aurora.Modules.Routing;
using Npgsql;
namespace Aurora.Api.Features.Routing;
public static class TmsEndpoints
{
 public static void MapTmsEndpoints(this IEndpointRouteBuilder app) {
  var g=app.MapGroup("/api/v1/tms").RequireAuthorization();
  g.MapGet("/orders",(DateTimeOffset? from,DateTimeOffset? to,string? status,string? search,Guid? manifest,TmsStore s,CancellationToken ct)=>Guard(async()=>Results.Ok(await s.Orders(from,to,status,search,manifest,ct))));
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
  catch(FormatException e){return Results.BadRequest(new ApiErrorDto(e.Message));}
  catch(KeyNotFoundException e){return Results.NotFound(new ApiErrorDto(e.Message));}
  catch(PostgresException e) when(e.SqlState=="23505"){return Results.Conflict(new ApiErrorDto("That number already exists, or the order is already assigned. Refresh and try again."));}
 }
}
