using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;

namespace Aurora.Integrations.FreightOps;

// One bounded scheduled pass. Credentials remain in the host's secret configuration.
public static class FreightOpsSyncCycle
{
    public static async Task Run(NpgsqlConnectionFactory factory,ITenantContext tenant,FreightOpsRuntimeOptions options,ISyncEntityStore entities,CancellationToken ct)
    {
        if(!options.Enabled)return;
        await using var lease=await factory.OpenConnectionAsync(ct);
        var lockName=$"aurora-background-sync:{tenant.TenantId}";
        if(!await lease.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT pg_try_advisory_lock(hashtextextended(@lockName,0))",new{lockName},cancellationToken:ct)))return;
        Connection? connection=null;
        try
        {
            connection=await lease.QuerySingleOrDefaultAsync<Connection>(new CommandDefinition("""
                SELECT id,credential_reference AS CredentialReference,source_identity AS SourceIdentity,checkpoint
                FROM aurora_sync_connection WHERE tenant_id=@TenantId AND provider='freightops' AND enabled
                """,new{tenant.TenantId},cancellationToken:ct));
            if(connection is null)return;
            await lease.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET last_attempt_at=now() WHERE tenant_id=@TenantId AND id=@Id",new{tenant.TenantId,connection.Id},cancellationToken:ct));
            var settings=options.Resolve(tenant.TenantId,connection.CredentialReference,connection.SourceIdentity);
            using var provider=new FreightOpsSyncProvider(connection.Id,settings.SourceIdentity,new Uri(settings.ApiUrl),new FreightOpsRequestStore(factory,tenant),_=>Task.FromResult(settings.AccessToken));
            var importer=new SyncImporter(factory,tenant,entities);
            var runner=new SyncRunner(new SyncStore(factory,tenant),provider,importer);
            // Import the target's current revision before constructing any outgoing request.
            try{await runner.Pull(connection.Id,connection.Checkpoint,ct);}
            catch(SyncBusyException)
            {
                // Recover expired/uncertain writes using the frozen target request.
                await runner.PushPending(connection.Id,ct);
                throw new FormatException("FreightOps import is waiting for an in-flight write; it will be retried.");
            }
            await runner.PushPending(connection.Id,ct);
            await lease.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET initialized_at=COALESCE(initialized_at,now()),last_error=NULL WHERE tenant_id=@TenantId AND id=@Id",new{tenant.TenantId,connection.Id},cancellationToken:ct));
        }
        catch(Exception)
        {
            if(connection is not null)
            {
                using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await lease.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET last_error='FreightOps sync could not finish. Check machine configuration, source generation and record issues; the worker will retry.' WHERE tenant_id=@TenantId AND id=@Id",new{tenant.TenantId,connection.Id},cancellationToken:cleanup.Token));
            }
            throw;
        }
        finally
        {
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await lease.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_unlock(hashtextextended(@lockName,0))",new{lockName},cancellationToken:cleanup.Token));
        }
    }
    sealed record Connection(Guid Id,Guid CredentialReference,string SourceIdentity,string? Checkpoint);
}
