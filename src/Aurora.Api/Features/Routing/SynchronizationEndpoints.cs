using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;

namespace Aurora.Api.Features.Routing;

public static class SynchronizationEndpoints
{
    public static void MapSynchronizationEndpoints(this IEndpointRouteBuilder app)
    {
        var g=app.MapGroup("/api/v1/synchronization").RequireAuthorization();
        g.MapGet("/mode",(IConfiguration config)=>new WorkspaceMode(config.GetValue<bool>("Synchronization:LocalWorkspace")));
        g.MapGet("/status",async (SyncReview review,NpgsqlConnectionFactory factory,ITenantContext tenant,CancellationToken ct)=>
        {
            var status=await review.Status(ct);
            await using var db=await factory.OpenConnectionAsync(ct);
            var detail=await db.QuerySingleOrDefaultAsync<Details>(new CommandDefinition("SELECT COALESCE(f.source_name,d.name,c.source_identity) AS Source,c.last_attempt_at AS LastAttempt,c.last_error AS Error FROM aurora_sync_connection c LEFT JOIN public.aurora_sync_credentials() f ON f.id=c.id LEFT JOIN nova_database d ON d.tenant_id=c.tenant_id AND d.id=c.credential_reference WHERE c.tenant_id=@TenantId",new{tenant.TenantId},cancellationToken:ct));
            return new SynchronizationStatus(status?.Enabled??false,detail?.Source??"Not connected",status?.Pending??0,status?.Working??0,status?.Conflicts??0,status?.LastSuccess,detail?.LastAttempt,detail?.Error);
        });
        var admin=g.MapGroup("").RequireAuthorization(p=>p.RequireRole("aurora:Admin","aurora:PowerUser"));
        admin.MapGet("/issues",async (NpgsqlConnectionFactory factory,ITenantContext tenant,CancellationToken ct)=>
        {
            await using var db=await factory.OpenConnectionAsync(ct);
            return await db.QueryAsync<SynchronizationIssue>(new CommandDefinition("SELECT id,entity_type AS EntityType,local_id AS LocalId,state,attempts,last_error AS Error FROM aurora_sync_outbox WHERE tenant_id=@TenantId AND state IN ('pending','conflict') AND last_error IS NOT NULL ORDER BY sequence LIMIT 200",new{tenant.TenantId},cancellationToken:ct));
        });
        admin.MapGet("/conflicts",(SyncReview review,CancellationToken ct)=>review.Conflicts(ct));
        admin.MapPost("/issues/{id:guid}/retry",(Guid id,NpgsqlConnectionFactory factory,ITenantContext tenant,CancellationToken ct)=>OrderWorkspaceEndpoints.Guard(async()=>
        {
            await using var db=await factory.OpenConnectionAsync(ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            await db.ExecuteAsync(new CommandDefinition("SELECT id FROM aurora_sync_connection WHERE tenant_id=@TenantId FOR NO KEY UPDATE",new{tenant.TenantId},tx,cancellationToken:ct));
            var issue=await db.QuerySingleOrDefaultAsync<IssueIdentity>(new CommandDefinition("SELECT entity_type AS Kind,local_id AS LocalId FROM aurora_sync_outbox WHERE tenant_id=@TenantId AND id=@id",new{tenant.TenantId,id},tx,cancellationToken:ct))??throw new KeyNotFoundException("Issue no longer exists.");
            var args=new{tenant.TenantId,issue.Kind,issue.LocalId};
            if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_sync_outbox WHERE tenant_id=@TenantId AND entity_type=@Kind AND local_id=@LocalId AND state='working') OR EXISTS(SELECT 1 FROM aurora_sync_conflict WHERE tenant_id=@TenantId AND entity_type=@Kind AND local_id=@LocalId AND resolved_at IS NULL)",args,tx,cancellationToken:ct)))throw new FormatException("Resolve the field conflict or wait for the current exchange before retrying.");
            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE aurora_sync_outbox o SET state='superseded',last_error=NULL
                WHERE tenant_id=@TenantId AND entity_type=@Kind AND local_id=@LocalId AND state IN ('pending','conflict')
                  AND EXISTS(SELECT 1 FROM aurora_sync_outbox newer WHERE newer.tenant_id=o.tenant_id
                    AND newer.connection_id=o.connection_id AND newer.entity_type=o.entity_type AND newer.local_id=o.local_id AND newer.sequence>o.sequence);
                UPDATE aurora_sync_outbox SET state='pending',available_at=now(),last_error=NULL
                WHERE tenant_id=@TenantId AND entity_type=@Kind AND local_id=@LocalId AND state IN ('pending','conflict');
                """,args,tx,cancellationToken:ct));
            await tx.CommitAsync(ct);return Results.NoContent();
        }));
        admin.MapPost("/conflicts/{id:guid}/resolve",(Guid id,SyncResolution input,SyncReview review,CancellationToken ct)=>OrderWorkspaceEndpoints.Guard(async()=>{await review.Resolve(id,input,ct);return Results.NoContent();}));
        admin.MapGet("/connection",(SyncConnections connections,CancellationToken ct)=>connections.Read(ct));
        admin.MapPut("/connection",(TenantIntegrationEdit input,SyncConnections connections,CancellationToken ct)=>OrderWorkspaceEndpoints.Guard(async()=>Results.Ok(await connections.Save(input,ct))))
            .RequireAuthorization(p=>p.RequireRole("aurora:Admin"));
    }
    private sealed record Details(string Source,DateTime? LastAttempt,string? Error);
    private sealed record IssueIdentity(string Kind,string LocalId);
}
