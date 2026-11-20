using Aurora.Contracts;

namespace Aurora.Client;

/// <summary>
/// Maps product codes to the route that shows them inside Aurora's shell.
///
/// External products load in a workspace frame beneath the shared header. Aurora prepares its
/// session cookie before the product's normal PKCE flow. Optimization is a native page.
/// </summary>
public static class ProductWorkspace
{
    public const string Aurora = "aurora";
    public const string FreightOps = "freightops";
    public const string Hub = "hub";
    public const string Nova = "nova";

    public static string RouteFor(string productCode) => productCode switch
    {
        FreightOps => "/workspace/freightops",
        Hub => "/workspace/hub",
        Nova => "/workspace/nova",
        _ => "/aurora/orders"
    };

    public static string? CodeFor(string path) => path switch
    {
        "/workspace/freightops" => FreightOps,
        "/workspace/hub" => Hub,
        "/workspace/nova" => Nova,
        "/" or "/routing" or "/aurora/route-optimization" or "/aurora/orders" or "/aurora/manifests" or "/aurora/fleet" or "/aurora/planning" => Aurora,
        _ => null
    };

    public static bool IsOptimizationPath(string path) => path == "/routing" ||
        (path.StartsWith("/aurora/", StringComparison.Ordinal) && path is not
            ("/aurora/users" or "/aurora/applog" or "/aurora/reset" or "/aurora/ship-status" or
             "/aurora/configuration" or "/aurora/document-conversion" or "/aurora/synchronization"));

    public static string? LandingRoute(TenantNavigationSettings settings, IReadOnlyList<ProductDto> products)
    {
        if (products.Any(p => p.Code == Aurora && p.HasAccess))
        {
            if (settings.Optimization) return RouteFor(Aurora);
            if (settings.DataEntry) return "/data-entry";
        }
        var product = products.FirstOrDefault(p => p.HasAccess && p.Code is (FreightOps or Hub or Nova) && settings.IsVisible(p.Code));
        return product is null ? null : RouteFor(product.Code);
    }

    /// <summary>Display name shown while an embedded product is loading.</summary>
    public static string NameFor(string productCode) => productCode switch
    {
        FreightOps => "FreightOps",
        Hub => "Integration Hub",
        Nova => "Nova",
        _ => "Optimization"
    };
}
