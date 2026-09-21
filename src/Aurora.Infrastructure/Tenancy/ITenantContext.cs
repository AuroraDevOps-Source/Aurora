namespace Aurora.Infrastructure.Tenancy;

public interface ITenantContext
{
    /// <summary>Null-safe accessor for connection-open plumbing — never throws.</summary>
    Guid? TenantIdOrNull { get; }

    /// <summary>
    /// The authenticated user's id, independent of tenant — resolves before a tenant is known
    /// (e.g. on the pre-auth cookie principal) and backs the "which tenants am I in" lookup,
    /// which by definition can't itself be gated by tenant (see user_tenant's RLS policy).
    /// </summary>
    Guid? UserIdOrNull { get; }

    /// <summary>Strict accessor for tenant-scoped application code — throws if no tenant is established (§3.5).</summary>
    Guid TenantId => TenantIdOrNull
        ?? throw new InvalidOperationException("No tenant_id claim is present on the current request.");

    /// <summary>Strict accessor for code that requires an authenticated user.</summary>
    Guid UserId => UserIdOrNull
        ?? throw new InvalidOperationException("No user id claim is present on the current request.");
}
