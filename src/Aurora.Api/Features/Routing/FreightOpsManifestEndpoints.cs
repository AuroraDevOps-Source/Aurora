using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.FreightOps;
using Aurora.Modules.Routing;
using Dapper;
using Microsoft.Extensions.Options;

namespace Aurora.Api.Features.Routing;

public static class FreightOpsManifestEndpoints
{
    public sealed record PublishRequest(int Revision);
    public static void MapFreightOpsManifests(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/aurora/manifests/{id:guid}/freightops").RequireAuthorization(p=>p.RequireRole("aurora:Admin","aurora:PowerUser")).AddEndpointFilter<NativeWorkspaceFilter>();
        group.MapPost("",(Guid id,PublishRequest input,NpgsqlConnectionFactory factory,ITenantContext tenant,IOptions<FreightOpsRuntimeOptions> runtime,CancellationToken ct)=>OrderWorkspaceEndpoints.Guard(async()=>
        {
            await using var db=await factory.OpenConnectionAsync(ct);
            var connection=await db.QuerySingleOrDefaultAsync<Connection>(new CommandDefinition("SELECT id,credential_reference AS CredentialReference,source_identity AS SourceIdentity FROM aurora_sync_connection WHERE tenant_id=@TenantId AND provider='freightops' AND enabled",new{tenant.TenantId},cancellationToken:ct))??throw new FormatException("Enable the FreightOps connection first.");
            if(!runtime.Value.Enabled||!runtime.Value.Resolve(tenant.TenantId,connection.CredentialReference,connection.SourceIdentity).ManifestPublishingEnabled)throw new FormatException("FreightOps manifest publishing is not enabled for this tenant.");
            await new FreightOpsManifestPublishing(factory,tenant).Publish(id,input.Revision,connection.Id,ct);
            return Results.Ok(new{message="Queued for FreightOps. Refresh delivery status to confirm."});
        }));
        group.MapGet("",async(Guid id,NpgsqlConnectionFactory factory,ITenantContext tenant,CancellationToken ct)=>
        {
            await using var db=await factory.OpenConnectionAsync(ct);
            return Results.Ok(await db.QuerySingleOrDefaultAsync<Delivery>(new CommandDefinition("""
                SELECT o.state AS State,o.last_error AS Error,m.external_id AS ExternalId,o.local_revision AS Revision
                FROM aurora_sync_outbox o JOIN aurora_sync_connection c ON (c.tenant_id,c.id)=(o.tenant_id,o.connection_id)
                LEFT JOIN aurora_sync_mapping m ON (m.tenant_id,m.connection_id,m.entity_type,m.local_id)=(o.tenant_id,o.connection_id,o.entity_type,o.local_id)
                WHERE o.tenant_id=@TenantId AND c.provider='freightops' AND o.entity_type='manifest' AND o.local_id=@localId ORDER BY o.sequence DESC LIMIT 1
                """,new{tenant.TenantId,localId=id.ToString()},cancellationToken:ct))??new Delivery("not-published",null,null,0));
        });
    }
    sealed record Connection(Guid Id,Guid CredentialReference,string SourceIdentity);
    sealed record Delivery(string State,string? Error,string? ExternalId,long Revision);
}
