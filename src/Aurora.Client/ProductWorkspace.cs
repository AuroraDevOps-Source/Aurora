namespace Aurora.Client;

/// <summary>
/// Maps product codes to the route that shows them inside Aurora's shell.
///
/// External products load in a workspace frame beneath the shared header. Aurora prepares its
/// session cookie before the product's normal PKCE flow. Route Optimization is a native page.
/// </summary>
public static class ProductWorkspace
{
    public const string Aurora = "aurora";
    public const string FreightOps = "freightops";
    public const string Hub = "hub";

    public static string RouteFor(string productCode) => productCode switch
    {
        FreightOps => "/workspace/freightops",
        Hub => "/workspace/hub",
        _ => "/routing"
    };

    public static string? CodeFor(string path) => path switch
    {
        "/workspace/freightops" => FreightOps,
        "/workspace/hub" => Hub,
        "/" or "/routing" => Aurora,
        _ => null
    };

    /// <summary>Display name shown while an embedded product is loading.</summary>
    public static string NameFor(string productCode) => productCode switch
    {
        FreightOps => "FreightOps",
        Hub => "Integration Hub",
        _ => "Route Optimization"
    };
}
