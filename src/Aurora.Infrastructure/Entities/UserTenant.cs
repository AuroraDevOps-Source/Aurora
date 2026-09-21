namespace Aurora.Infrastructure.Entities;

/// <summary>
/// A user can belong to more than one tenant (§6.2) — this is the membership fact.
/// Exactly one row per user has <see cref="IsDefault"/> set, used to pick the tenant
/// a token is issued for until a tenant-picker UI exists.
/// </summary>
public sealed class UserTenant
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset JoinedUtc { get; set; }
}
