using Aurora.Contracts;
using Aurora.Modules.Routing;

namespace Aurora.Api.Features.Routing;

// Legacy bookmarks must never bypass the local workspace and write directly to Nova.
public sealed class LocalWorkspaceFilter(IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,EndpointFilterDelegate next)
    {
        if(!configuration.GetValue<bool>("Synchronization:LocalWorkspace"))return await next(context);
        var http=context.HttpContext;
        var path=http.Request.Path.Value!;
        if(!path.StartsWith("/api/v1/aurora/firebird",StringComparison.Ordinal))return await next(context);
        var services=http.RequestServices;var ct=http.RequestAborted;
        if(path=="/api/v1/aurora/firebird/routing-configuration")return await OrderWorkspaceEndpoints.Guard(async()=>
        {
            var config=services.GetRequiredService<LocalRoutingConfiguration>();
            if(HttpMethods.IsGet(http.Request.Method))return Results.Ok(await config.Read(ct));
            await config.Save(context.Arguments.OfType<NovaRoutingConfigurationEdit>().Single(),ct);return Results.NoContent();
        });
        if(path=="/api/v1/aurora/firebird" && HttpMethods.IsGet(http.Request.Method))return Results.Ok(new FirebirdRoutingStatus(false));
        if(path=="/api/v1/aurora/firebird/terminals" && HttpMethods.IsGet(http.Request.Method))return Results.Ok(await services.GetRequiredService<TerminalStore>().List(ct));
        if(path=="/api/v1/aurora/firebird/fleet" && HttpMethods.IsGet(http.Request.Method))return Results.Ok(await services.GetRequiredService<EquipmentStore>().List(ct));
        if(path=="/api/v1/aurora/firebird/drivers" && HttpMethods.IsGet(http.Request.Method))return Results.Ok(new DriverDirectory(await services.GetRequiredService<DriverStore>().List(ct),[],(await services.GetRequiredService<TerminalStore>().List(ct)).Select(t=>t.Code).ToArray()));
        if(path.StartsWith("/api/v1/aurora/firebird/drivers/",StringComparison.Ordinal))
            return await OrderWorkspaceEndpoints.Guard(async()=>
            {
                var id=long.Parse(http.Request.RouteValues["id"]!.ToString()!);
                var store=services.GetRequiredService<DriverStore>();
                if(HttpMethods.IsPut(http.Request.Method))return Results.Ok(await store.Save(id,context.Arguments.OfType<NovaDriver>().Single(),ct));
                if(HttpMethods.IsDelete(http.Request.Method)){await store.Delete(id,http.Request.Query["revision"].ToString(),ct);return Results.NoContent();}
                return Results.StatusCode(405);
            });
        return Results.Conflict(new ApiErrorDto("This workspace now uses Aurora's local records. Reload the workspace to continue. Nova updates run through background synchronization."));
    }
}
