using System.Text.Json.Nodes;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.FreightOps;
using Dapper;
using Microsoft.Extensions.Options;

namespace Aurora.Api.Features.Routing;

public sealed record FreightOpsEnrollmentRequest(Guid ChangeId,string Pro,string ExpectedRevision,JsonObject Data);
public static class FreightOpsEnrollment
{
    public static void MapFreightOpsEnrollment(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/synchronization/freightops/enroll",(FreightOpsEnrollmentRequest input,NpgsqlConnectionFactory factory,ITenantContext tenant,IOptions<FreightOpsRuntimeOptions> runtime,CancellationToken ct)=>OrderWorkspaceEndpoints.Guard(async()=>
        {
            if(!runtime.Value.Enabled)throw new FormatException("FreightOps machine integration is disabled.");
            if(input.ChangeId==Guid.Empty||string.IsNullOrWhiteSpace(input.Pro)||input.Data is null||
                input.Data["Id"] is not JsonValue id||!id.TryGetValue<string>(out var pro)||pro!=input.Pro)
                throw new FormatException("Provide the exact PRO snapshot and a stable enrollment change ID.");
            await using var db=await factory.OpenConnectionAsync(ct);
            var connection=await db.QuerySingleOrDefaultAsync<Connection>(new CommandDefinition("SELECT id,credential_reference AS CredentialReference,source_identity AS SourceIdentity FROM aurora_sync_connection WHERE tenant_id=@TenantId AND provider='freightops'",new{tenant.TenantId},cancellationToken:ct))
                ??throw new FormatException("Save the tenant's FreightOps connection before enrollment.");
            var settings=runtime.Value.Resolve(tenant.TenantId,connection.CredentialReference,connection.SourceIdentity);
            using var provider=new FreightOpsSyncProvider(connection.Id,settings.SourceIdentity,new Uri(settings.ApiUrl),new FreightOpsRequestStore(factory,tenant),_=>Task.FromResult(settings.AccessToken));
            var request=new FreightOpsWrite(1,settings.SourceIdentity,connection.Id,input.ChangeId,"order",input.Pro,0,"enroll",input.Pro,input.ExpectedRevision,input.Data);
            try{return Results.Ok(await provider.Enroll(request,ct));}
            catch(Aurora.Modules.Sync.SyncConflictException ex){return Results.Conflict(new{error=ex.Message});}
            catch(HttpRequestException){return Results.Json(new{error="Enrollment was not confirmed. Retry the identical change ID and body; do not create a new enrollment request."},statusCode:503);}
            catch(OperationCanceledException) when(!ct.IsCancellationRequested){return Results.Json(new{error="Enrollment timed out. Retry the identical change ID and body."},statusCode:503);}
        })).RequireAuthorization(p=>p.RequireRole("aurora:Admin"));
    }
    sealed record Connection(Guid Id,Guid CredentialReference,string SourceIdentity);
}
