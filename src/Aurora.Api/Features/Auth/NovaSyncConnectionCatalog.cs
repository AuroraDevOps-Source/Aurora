using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;

namespace Aurora.Api.Features.Auth;

// The host resolves the credential reference; core synchronization never receives
// the Firebird connection string or knows the legacy user/database schema.
public sealed class NovaSyncConnectionCatalog(NpgsqlConnectionFactory factory,ITenantContext tenant,NovaUserConnections connections) : IIntegrationConnectionCatalog
{
    public async Task<string> Validate(string provider,Guid credentialReference,CancellationToken ct)
    {
        if(provider!="nova")throw new FormatException("Select a supported integration.");
        await using var db=await factory.OpenConnectionAsync(ct);
        var json=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT connection::text FROM nova_database WHERE tenant_id=@TenantId AND id=@credentialReference",new{tenant.TenantId,credentialReference},cancellationToken:ct));
        var saved=connections.Read(json)??throw new FormatException("Select a database connection belonging to this tenant.");
        return saved.SourceKey;
    }
}
