using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Api.Features.Routing;

public sealed class LocalRoutingConfiguration(NpgsqlConnectionFactory factory,ITenantContext tenant)
{
    public async Task<NovaRoutingConfiguration> Read(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT c.source_identity AS Source,COALESCE(s.ready_statuses,ARRAY[110]) AS Ready,
              COALESCE(s.pickup_ready_statuses,ARRAY[]::integer[]) AS Pickups,c.metadata::text AS Metadata
            FROM aurora_sync_connection c LEFT JOIN aurora_routing_status_configuration s
              ON s.tenant_id=c.tenant_id AND s.source_key=c.source_identity WHERE c.tenant_id=@TenantId
            """,new{tenant.TenantId},cancellationToken:ct))??throw new FormatException("Assign a tenant source before changing planning statuses.");
        using var metadata=JsonDocument.Parse(row.Metadata);
        var available=metadata.RootElement.TryGetProperty("AvailableStatuses",out var list)?list.Deserialize<NovaManifestStatus[]>()??[]:[];
        return new(row.Ready,available){PickupReadyStatuses=row.Pickups};
    }
    public async Task Save(NovaRoutingConfigurationEdit input,CancellationToken ct)
    {
        var current=await Read(ct);
        if(input.ReadyStatuses is not {Length:>0 and <=200} || input.PickupReadyStatuses is not {Length:<=200}
            || input.ReadyStatuses.Intersect(input.PickupReadyStatuses).Any()
            || input.ReadyStatuses.Concat(input.PickupReadyStatuses).Except(current.AvailableStatuses.Select(s=>s.Id)).Any())throw new FormatException("Choose separate, valid delivery and pickup statuses.");
        await using var db=await factory.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_routing_status_configuration(tenant_id,source_key,ready_statuses,pickup_ready_statuses)
            SELECT tenant_id,source_identity,@ready,@pickups FROM aurora_sync_connection WHERE tenant_id=@TenantId
            ON CONFLICT(tenant_id,source_key) DO UPDATE SET ready_statuses=excluded.ready_statuses,pickup_ready_statuses=excluded.pickup_ready_statuses
            """,new{tenant.TenantId,ready=input.ReadyStatuses.Distinct().Order().ToArray(),pickups=input.PickupReadyStatuses.Distinct().Order().ToArray()},cancellationToken:ct));
    }
    private sealed class Row
    {
        public string Source {get;set;}="";
        public int[] Ready {get;set;}=[];
        public int[] Pickups {get;set;}=[];
        public string Metadata {get;set;}="{}";
    }
}
