using System.Data.Common;
using Aurora.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Aurora.Infrastructure.Data;

public sealed class TenantConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is NpgsqlConnection npgsqlConnection)
        {
            await TenantConnectionSetup.ApplyAsync(npgsqlConnection, tenantContext, cancellationToken);
        }
    }
}
