using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Npgsql;

namespace Aurora.Api.Features.Routing;

public interface ITenantSyncExecutor
{
    Task Run(Guid tenantId, CancellationToken ct);
}

/// <summary>Runs independently of logged-in users. Each cycle owns an explicit tenant scope.</summary>
public sealed class IntegrationSyncWorker(IServiceScopeFactory scopes, NpgsqlDataSource dataSource,
    ILogger<IntegrationSyncWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Synchronization:Enabled")) return;
        var interval = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Synchronization:IntervalSeconds", 30), 5, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // This function returns IDs only, never credentials or business data. All
                // subsequent queries use the normal tenant-scoped connection factory.
                await using var directory = await new NpgsqlConnectionFactory(dataSource, new WorkerTenant(null)).OpenConnectionAsync(stoppingToken);
                var tenants = (await directory.QueryAsync<Guid>(new CommandDefinition(
                    "SELECT tenant_id FROM public.aurora_enabled_sync_tenants()", cancellationToken: stoppingToken))).ToArray();
                foreach (var tenantId in tenants)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await using var scope = scopes.CreateAsyncScope();
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(TimeSpan.FromMinutes(5));
                    try { await scope.ServiceProvider.GetRequiredService<ITenantSyncExecutor>().Run(tenantId, timeout.Token); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        // Provider exceptions may include SQL or connection details. Status
                        // persisted by the executor contains a safe, actionable message.
                        logger.LogWarning("Synchronization cycle failed for tenant {TenantId}; it will retry.", tenantId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Synchronization directory is unavailable; the worker will retry."); }
            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

internal sealed record WorkerTenant(Guid? TenantIdOrNull) : ITenantContext
{
    public Guid? UserIdOrNull => null;
}
