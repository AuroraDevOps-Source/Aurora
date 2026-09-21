using Microsoft.AspNetCore.Identity;

namespace Aurora.Infrastructure.Entities;

/// <summary>
/// Role assignments are per-tenant: the same person can be Admin in one tenant and
/// Dispatcher in another, matching the token contract's "roles within that tenant".
/// </summary>
public sealed class ApplicationUserRole : IdentityUserRole<Guid>
{
    public Guid TenantId { get; set; }
}
