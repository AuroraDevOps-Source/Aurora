using Aurora.Infrastructure.Tenancy;
using Npgsql;

namespace Aurora.Infrastructure.Data;

/// <summary>
/// Single implementation of "set the Postgres session's tenant" shared by the EF Core
/// connection interceptor and the Dapper connection factory, so both data-access paths
/// enforce the same RLS context (§3.3).
/// </summary>
public static class TenantConnectionSetup
{
    public static async Task ApplyAsync(
        NpgsqlConnection connection,
        ITenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', $1, false), set_config('app.user_id', $2, false)";

        var tenantParameter = command.CreateParameter();
        tenantParameter.Value = (object?)tenantContext.TenantIdOrNull?.ToString() ?? DBNull.Value;
        command.Parameters.Add(tenantParameter);

        var userParameter = command.CreateParameter();
        userParameter.Value = (object?)tenantContext.UserIdOrNull?.ToString() ?? DBNull.Value;
        command.Parameters.Add(userParameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
