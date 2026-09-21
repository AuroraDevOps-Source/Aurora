using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Routing.Queries.ListRoutePlans;

public sealed class ListRoutePlansQuery(NpgsqlConnectionFactory connectionFactory, ITenantContext tenantContext)
{
    private static readonly string Sql = LoadSql();

    public async Task<IReadOnlyList<RoutePlanDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            Sql,
            new { TenantId = tenantContext.TenantId },
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<RoutePlanDto>(command);
        return rows.AsList();
    }

    private static string LoadSql()
    {
        const string resourceName = "Aurora.Modules.Routing.Queries.ListRoutePlans.ListRoutePlansQuery.sql";
        var assembly = typeof(ListRoutePlansQuery).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
