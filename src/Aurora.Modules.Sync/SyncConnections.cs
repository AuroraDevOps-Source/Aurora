using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
namespace Aurora.Modules.Sync;

public interface IIntegrationConnectionCatalog
{
    Task<string> Validate(string provider,Guid credentialReference,CancellationToken ct);
}

public sealed class SyncConnections(NpgsqlConnectionFactory factory,ITenantContext tenant,IIntegrationConnectionCatalog catalog)
{
    public async Task<TenantIntegration?> Read(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<TenantIntegration>(new CommandDefinition("SELECT id,provider,credential_reference AS CredentialReference,source_identity AS SourceIdentity,enabled,revision FROM aurora_sync_connection WHERE tenant_id=@TenantId",new{tenant.TenantId},cancellationToken:ct));
    }

    public async Task<TenantIntegration> Save(TenantIntegrationEdit input,CancellationToken ct)
    {
        var identity=await catalog.Validate(input.Provider,input.CredentialReference,ct);
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"sync-connection:{tenant.TenantId}"},tx,cancellationToken:ct));
        var current=await db.QuerySingleOrDefaultAsync<TenantIntegration>(new CommandDefinition("SELECT id,provider,credential_reference AS CredentialReference,source_identity AS SourceIdentity,enabled,revision FROM aurora_sync_connection WHERE tenant_id=@TenantId FOR UPDATE",new{tenant.TenantId},tx,cancellationToken:ct));
        if(input.Revision!=(current?.Revision??0))throw new FormatException("The tenant connection changed. Reload before saving.");
        if(current is not null && (current.SourceIdentity!=identity || current.Provider!=input.Provider))
            throw new FormatException("Changing the source database requires a separate data migration. Disable the current connection first and retain its record mappings.");
        var saved=new TenantIntegration(current?.Id??Guid.NewGuid(),input.Provider,input.CredentialReference,identity,input.Enabled,(current?.Revision??0)+1);
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_sync_connection(tenant_id,id,provider,credential_reference,source_identity,enabled,revision)
            VALUES(@TenantId,@Id,@Provider,@CredentialReference,@SourceIdentity,@Enabled,@Revision)
            ON CONFLICT(tenant_id) DO UPDATE SET credential_reference=excluded.credential_reference,enabled=excluded.enabled,revision=excluded.revision
            """,new{tenant.TenantId,saved.Id,saved.Provider,saved.CredentialReference,saved.SourceIdentity,saved.Enabled,saved.Revision},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return saved;
    }
}
