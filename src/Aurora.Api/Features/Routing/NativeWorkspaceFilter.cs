using Aurora.Contracts;

namespace Aurora.Api.Features.Routing;

// A stale local-workspace page must not write a second operational record after cutover.
public sealed class NativeWorkspaceFilter(IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,EndpointFilterDelegate next)
    {
        if(configuration.GetValue<bool>("Synchronization:LocalWorkspace"))return await next(context);
        var request=context.HttpContext.Request;
        if(!Blocks(request.Method,request.Path.Value??""))return await next(context);
        return Results.Conflict(new ApiErrorDto("This company uses its Nova database. Reload Aurora and save through the Nova order, manifest, or fleet screen."));
    }

    public static bool Blocks(string method,string path)
    {
        if(HttpMethods.IsGet(method)||HttpMethods.IsHead(method)||HttpMethods.IsOptions(method))return false;
        const string historyPrefix="/api/v1/tms/orders/import/history/";
        const string countsSuffix="/counts";
        if (HttpMethods.IsPut(method) && path.StartsWith(historyPrefix,StringComparison.Ordinal) &&
            path.EndsWith(countsSuffix,StringComparison.Ordinal) && path.Length>historyPrefix.Length+countsSuffix.Length &&
            Guid.TryParseExact(path[historyPrefix.Length..^countsSuffix.Length],"D",out _)) return false;
        if(path.EndsWith("/lookup-address",StringComparison.Ordinal)||path is "/api/v1/tms/orders/convert-document" or "/api/v1/tms/orders/convert-document/jobs"||path is "/api/v1/tms/orders/import/check" or "/api/v1/tms/orders/import/addresses" or "/api/v1/tms/orders/import/mapping-rule")return false;
        return path.StartsWith("/api/v1/tms/",StringComparison.Ordinal)
            ||path.StartsWith("/api/v1/routing/equipment/",StringComparison.Ordinal)
            ||path.StartsWith("/api/v1/aurora/manifests/",StringComparison.Ordinal)&&path.EndsWith("/freightops",StringComparison.Ordinal);
    }
}
