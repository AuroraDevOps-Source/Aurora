using Aurora.Api.Features.Auth;
using Aurora.Infrastructure.Data;
using Aurora.Integrations.Nova;
using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Aurora.Integrations.FreightOps;

namespace Aurora.Api.Features.Routing;

public sealed class TenantSyncExecutor(NpgsqlDataSource dataSource,NovaUserConnections credentials,IOptions<FreightOpsRuntimeOptions>? freightOps=null,IConfiguration? configuration=null) : ITenantSyncExecutor
{
    private bool IntegrationStaging => configuration is not null && !configuration.GetValue<bool>("Synchronization:LocalWorkspace");
    public async Task Run(Guid tenantId,CancellationToken ct)
    {
        Exception? failure=null;
        try{await RunNova(tenantId,ct);}
        catch(Exception ex) when(ex is not OperationCanceledException){failure=ex;}
        var tenant=new WorkerTenant(tenantId);
        var factory=new NpgsqlConnectionFactory(dataSource,tenant);
        try{await FreightOpsSyncCycle.Run(factory,tenant,freightOps?.Value??new(),new OperationalSyncEntities(tenant,new DriverStore(factory,tenant),IntegrationStaging),ct);}
        catch(Exception ex) when(ex is not OperationCanceledException){failure??=ex;}
        if(failure is not null)throw new InvalidOperationException("One or more integration passes failed; inspect each connection's status.",failure);
    }
    private async Task RunNova(Guid tenantId,CancellationToken ct)
    {
        var tenant=new WorkerTenant(tenantId);
        var factory=new NpgsqlConnectionFactory(dataSource,tenant);
        await using var lease=await factory.OpenConnectionAsync(ct);
        var lockName=$"aurora-background-sync:{tenantId}";
        if(!await lease.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT pg_try_advisory_lock(hashtextextended(@lockName,0))",new{lockName},cancellationToken:ct)))return;
        try
        {
            var connection=await lease.QuerySingleOrDefaultAsync<Connection>(new CommandDefinition("""
                SELECT f.id,f.checkpoint,f.credentials,c.metadata::text AS Metadata FROM public.aurora_sync_credentials() f JOIN aurora_sync_connection c ON c.id=f.id AND c.tenant_id=@tenantId
                """,new{tenantId},cancellationToken:ct));
            if(connection is null)return; // A tenant may have only FreightOps enabled.
            await lease.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET last_attempt_at=now() WHERE tenant_id=@tenantId AND provider='nova'",new{tenantId},cancellationToken:ct));
            var options=credentials.Resolve(credentials.Read(connection.Credentials),tenantId);
            options.IntegrationStaging=IntegrationStaging;
            var source=new FirebirdRoutingSource(factory,tenant,Options.Create(options));
            var config=await source.RoutingConfiguration(ct);
            var metadata=System.Text.Json.JsonSerializer.Serialize(new{config.AvailableStatuses,config.ReadyStatuses,config.PickupReadyStatuses,options.TimeZoneId,options.Lykes.PilotOrderIds,options.Lykes.ManifestSyncFromId,options.Lykes.OrderSyncFromId,options.Lykes.AllowHeaderTotals,options.Lykes.OperationalWritesEnabled,options.Lykes.OperationalOrderCreationEnabled,options.Lykes.DispatchSynchronizationEnabled});
            var mapper=source.ForSynchronization(connection.Id);
            var provider=new NovaSyncProvider(connection.Id,options.ConnectionString,mapper,timeZone:TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId));
            if(!IntegrationStaging)await NovaLegacyAdoption.Apply(factory,tenant,connection.Id,options.SourceKey,ct);
            await NovaSyncBootstrap.EnsureSeeded(options.ConnectionString,connection.Id,ct);
            if(!System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(connection.Metadata),System.Text.Json.Nodes.JsonNode.Parse(metadata)))await NovaSyncBootstrap.ReclassifyOrders(options.ConnectionString,connection.Id,ct,options.Lykes.ManifestSyncFromId is >0);
            await lease.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET metadata=CAST(@metadata AS jsonb) WHERE tenant_id=@tenantId AND provider='nova'",new{tenantId,metadata},cancellationToken:ct));
            var entities=new OperationalSyncEntities(tenant,new DriverStore(factory,tenant),IntegrationStaging);
            var importer=new SyncImporter(factory,tenant,entities,externalAuthoritative:IntegrationStaging);
            var runner=new SyncRunner(new SyncStore(factory,tenant),provider,importer);
            // Pull first to detect native edits before pushing any queued local changes.
            var checkpoint=connection.Checkpoint;
            for(var batch=0;batch<50;batch++)
            {
                try{await runner.Pull(connection.Id,checkpoint,ct);}
                catch(SyncBusyException)
                {
                    // Each outbound write still verifies its locked native baseline.
                    // Let pending dependencies finish instead of starving them forever.
                    if(!(options.Lykes.Enabled&&options.Lykes.NativeReadOnly&&!options.Lykes.OperationalWritesEnabled))await runner.PushPending(connection.Id,ct);
                    throw new FormatException("Incoming manifest membership is waiting for pending order changes or conflict review.");
                }
                if(await NovaSyncBootstrap.Pending(options.ConnectionString,connection.Id,ct)==0)
                {
                    await lease.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET initialized_at=COALESCE(initialized_at,now()) WHERE tenant_id=@tenantId AND provider='nova'",new{tenantId},cancellationToken:ct));
                    break;
                }
                checkpoint=await lease.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT checkpoint FROM aurora_sync_connection WHERE tenant_id=@tenantId AND provider='nova'",new{tenantId},cancellationToken:ct));
            }
            if(!(options.Lykes.Enabled&&options.Lykes.NativeReadOnly&&!options.Lykes.OperationalWritesEnabled))await runner.PushPending(connection.Id,ct);
            await lease.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET last_error=NULL WHERE tenant_id=@tenantId AND provider='nova'",new{tenantId},cancellationToken:ct));
        }
        catch(Exception ex)
        {
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await lease.ExecuteAsync(new CommandDefinition("UPDATE aurora_sync_connection SET last_error=@error WHERE tenant_id=@tenantId AND provider='nova'",new{tenantId,error=ex is FormatException?ex.Message[..Math.Min(500,ex.Message.Length)]:"Synchronization could not finish. Check connection availability and pending record issues; the worker will retry."},cancellationToken:cleanup.Token));
            throw;
        }
        finally
        {
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await lease.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_unlock(hashtextextended(@lockName,0))",new{lockName},cancellationToken:cleanup.Token));
        }
    }
    private sealed record Connection(Guid Id,string? Checkpoint,string Credentials,string Metadata);
}
