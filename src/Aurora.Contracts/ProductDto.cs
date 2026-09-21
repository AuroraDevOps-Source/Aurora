namespace Aurora.Contracts;

/// <summary>
/// One tile on the launcher: a product this tenant has bought.
/// <paramref name="Url"/> is where to send the user — for FreightOps that is this tenant's own
/// deployment, since it is installed once per customer rather than shared.
/// <paramref name="HasAccess"/> is false when the tenant owns the product but this particular
/// user holds no role in it, so the tile can be shown greyed out rather than hidden.
/// </summary>
public sealed record ProductDto(
    string Code,
    string Name,
    string? Description,
    string? Url,
    bool HasAccess);
