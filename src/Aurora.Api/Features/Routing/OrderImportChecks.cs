using System.Globalization;
using Aurora.Api.Features.Auth;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Api.Features.Routing;

public static class OrderImportChecks
{
    public static async Task<string[]> Existing(string[] ids, TmsStore store, NpgsqlConnectionFactory factory,
        ITenantContext tenant, NovaUserConnections connections, CancellationToken ct)
    {
        if (ids is not { Length: > 0 and <= 100 }) throw new FormatException("Check 1–100 PRO numbers at a time.");
        var numbers = ids.Select(id => OrderDocumentDraft.SplitTag(id).Pro).Distinct().ToArray();
        var existing = (await store.ExistingOrderNumbers(numbers, ct)).ToHashSet(StringComparer.Ordinal);
        // Query the tenant's source directly: the sync window/allowlist may not have imported an old PRO.
        string? saved;
        await using (var db = await factory.OpenConnectionAsync(ct))
            saved = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                "SELECT nova::text FROM tenant_settings WHERE tenant_id=@TenantId", new { tenant.TenantId }, cancellationToken: ct));
        var settings = connections.Read(saved);
        if (settings is not null)
        {
            var options = connections.Resolve(settings, tenant.TenantId);
            await using var nova = new FbConnection(options.ConnectionString);
            await nova.OpenAsync(ct);
            var found = await nova.QueryAsync<long>(new CommandDefinition(
                "SELECT DISTINCT CAST(PBNUM AS BIGINT) FROM OPS_HEADER WHERE PB_TYPE='O' AND PBNUM IN @pros",
                new { pros = numbers.Select(n => long.Parse(n, CultureInfo.InvariantCulture)).ToArray() }, cancellationToken: ct));
            existing.UnionWith(found.Select(n => n.ToString(CultureInfo.InvariantCulture)));
        }
        return existing.Order().ToArray();
    }
}
