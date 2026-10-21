using Aurora.Contracts;
using Aurora.Modules.Routing;
using Aurora.Api.Features.Auth;

namespace Aurora.Api.Features.Routing;

public static class WorkspaceResetEndpoints
{
    public static void MapWorkspaceResetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/reset").RequireAuthorization(p => p.RequireRole("aurora:Admin"));
        group.MapGet("/", (WorkspaceResetStore store, CancellationToken ct) => OrderWorkspaceEndpoints.Guard(async () => Results.Ok(await store.Preview(ct))));
        group.MapPost("/", (WorkspaceResetCommand input, WorkspaceResetStore store, PlanningSessions sessions, HttpContext context) =>
            OrderWorkspaceEndpoints.Guard(async () =>
            {
                // Finish even if the browser disconnects; do not abandon cross-database cleanup halfway.
                var result = await sessions.ResetWorkspace(() => store.Execute(input, CancellationToken.None));
                context.Items[AppLog.ActionKey] = "Reset test manifests and planning";
                context.Items[AppLog.DetailKey] = $"Deleted {result.Manifests} manifests and {result.Drafts} drafts. Returned {result.Orders} orders to routing.";
                return Results.Ok(result);
            }));
    }
}
