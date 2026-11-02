using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;
using Aurora.Integrations.FreightOps;
using Microsoft.Extensions.Options;

namespace Aurora.Api.Features.Auth;

// The host resolves the credential reference; core synchronization never receives
// the Firebird connection string or knows the legacy user/database schema.
public sealed class NovaSyncConnectionCatalog(NpgsqlConnectionFactory factory,ITenantContext tenant,NovaUserConnections connections,IOptions<FreightOpsRuntimeOptions>? freightOps=null) : IIntegrationConnectionCatalog
{
    public async Task<string> Validate(string provider,Guid credentialReference,CancellationToken ct)
    {
        if(provider=="freightops")return (freightOps?.Value??new()).Resolve(tenant.TenantId,credentialReference).SourceIdentity;
        if(provider!="nova")throw new FormatException("Select a supported integration.");
        await using var db=await factory.OpenConnectionAsync(ct);
        var json=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT nova::text FROM tenant_settings WHERE tenant_id=@TenantId AND nova_id=@credentialReference",new{tenant.TenantId,credentialReference},cancellationToken:ct));
        var saved=connections.Read(json)??throw new FormatException("Select a database connection belonging to this tenant.");
        return saved.SourceKey;
    }
}
