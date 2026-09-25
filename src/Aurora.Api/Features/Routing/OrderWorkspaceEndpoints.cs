using Aurora.Contracts;
using Aurora.Modules.Routing;

namespace Aurora.Api.Features.Routing;

public static class OrderWorkspaceEndpoints
{
    public static void MapOrderWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/aurora").RequireAuthorization();
        group.MapGet("/sources", (OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Sources(ct))));
        group.MapGet("/orders", (Guid source,DateTimeOffset? from,DateTimeOffset? to,string? status,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Orders(source,from,to,status,ct))));
        group.MapPost("/imports", (SeedOrdersDto input,OrderWorkspace store,CancellationToken ct)=>Guard(async()=> { await store.Import(input,ct); return Results.Ok(); }))
            .RequireAuthorization(p=>p.RequireRole("aurora:Admin"))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(60*1024*1024));
        group.MapPost("/drafts", (CreateOrderDraftDto input,HttpContext context,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.CreateDraft(input,PlanningSessions.Owner(context.User),ct))));
        group.MapGet("/drafts/{id:guid}", (Guid id,HttpContext context,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Draft(id,PlanningSessions.Owner(context.User),ct))));
        group.MapGet("/manifests", (OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.Manifests(ct))));
        group.MapPut("/manifests/{id:guid}", (Guid id,UpdateManifestDto input,OrderWorkspace store,CancellationToken ct)=>Guard(async()=>Results.Ok(await store.UpdateManifest(id,input,ct))));
        group.MapPost("/drafts/{id:guid}/finish", (Guid id,FinishOrderDraftDto input,HttpContext context,OrderWorkspace store,PlanningSessions sessions,CancellationToken ct)=>Guard(async()=>
        {
            var result=await sessions.Completed(input.SessionId,id,context.User,ct);
            return Results.Ok(await store.Finish(id,input.SessionId,PlanningSessions.Owner(context.User),result,ct));
        }));
    }
    private static async Task<IResult> Guard(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch(FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
        catch(KeyNotFoundException ex) { return Results.NotFound(new ApiErrorDto(ex.Message)); }
    }
}
