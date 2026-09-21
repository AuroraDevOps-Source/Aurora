using Aurora.Infrastructure.Tenancy;
using Npgsql;

namespace Aurora.Infrastructure.Data;

/// <summary>
/// Required connection-open path for every Dapper query handler — never open a raw
/// <see cref="NpgsqlConnection"/> directly, or the tenant RLS context won't be set (§3.3).
/// </summary>
public sealed class NpgsqlConnectionFactory(NpgsqlDataSource dataSource, ITenantContext tenantContext)
{
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await TenantConnectionSetup.ApplyAsync(connection, tenantContext, cancellationToken);
        return connection;
    }
}
