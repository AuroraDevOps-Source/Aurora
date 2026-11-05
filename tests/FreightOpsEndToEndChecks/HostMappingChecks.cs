using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Aurora.Api.Features.Auth;
using Aurora.Api.Features.Routing;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.FreightOps;
using Aurora.Integrations.Nova;
using Aurora.Modules.Sync;
using Dapper;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Npgsql;

static class HostMappingChecks
{
    public static async Task Run(NpgsqlDataSource dataSource,NpgsqlConnectionFactory factory,ITenantContext tenant,ISyncEntityStore entities,FreightOpsRuntimeOptions runtime,IDocumentStore targetStore,Action<bool,string> check)
    {
        await using var db=await factory.OpenConnectionAsync();
        // Exercise the real host with only FreightOps enabled; the Nova credential
        // directory correctly returns no enabled Nova source in this fixture.
        await db.ExecuteAsync("""
            UPDATE aurora_sync_connection SET enabled=false WHERE provider='nova';
            CREATE FUNCTION public.aurora_sync_credentials() RETURNS TABLE(id uuid,checkpoint text,credentials text,source_name text)
            LANGUAGE sql AS $$ SELECT NULL::uuid,NULL::text,NULL::text,NULL::text WHERE false $$;
            """);
        var builder=WebApplication.CreateSlimBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{{"Synchronization:LocalWorkspace","true"},{"Synchronization:Enabled","true"},{"Synchronization:FreightOps:Enabled","true"}});
        builder.Services.AddSingleton(dataSource);builder.Services.AddSingleton(factory);builder.Services.AddSingleton(tenant);builder.Services.AddSingleton(entities);
        builder.Services.AddOptions<FirebirdRoutingOptions>();builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddSingleton<IOptions<FreightOpsRuntimeOptions>>(Options.Create(runtime));
        builder.Services.AddSingleton<NovaUserConnections>();builder.Services.AddSingleton<IIntegrationConnectionCatalog,NovaSyncConnectionCatalog>();
        builder.Services.AddSingleton<SyncConnections>();builder.Services.AddSingleton<SyncReview>();builder.Services.AddSingleton<TenantSyncExecutor>();
        builder.Services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions,FixtureAuth>("fixture",_=>{});builder.Services.AddAuthorization();
        await using var app=builder.Build();app.UseAuthentication();app.UseAuthorization();app.MapSynchronizationEndpoints();await app.StartAsync();
        await app.Services.GetRequiredService<TenantSyncExecutor>().Run(tenant.TenantId,default);
        check(await db.ExecuteScalarAsync<DateTime?>("SELECT last_attempt_at FROM aurora_sync_connection WHERE provider='freightops'") is not null,"actual tenant executor runs FreightOps when Nova is disabled");
        await db.ExecuteAsync("""
            UPDATE aurora_sync_connection SET last_attempt_at=NULL WHERE provider='freightops';
            CREATE OR REPLACE FUNCTION public.aurora_sync_credentials() RETURNS TABLE(id uuid,checkpoint text,credentials text,source_name text)
            LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture source unavailable'; END $$;
            """);
        try{await app.Services.GetRequiredService<TenantSyncExecutor>().Run(tenant.TenantId,default);throw new Exception("Missing provider failure");}
        catch(InvalidOperationException){check(await db.ExecuteScalarAsync<DateTime?>("SELECT last_attempt_at FROM aurora_sync_connection WHERE provider='freightops'") is not null,"Nova failure does not starve the FreightOps worker pass");}
        check(await db.ExecuteScalarAsync<string>("SELECT last_error FROM aurora_sync_connection WHERE provider='nova'") is not null&&
            await db.ExecuteScalarAsync<string>("SELECT last_error FROM aurora_sync_connection WHERE provider='freightops'") is null,"provider failure statuses remain independent");
        await db.ExecuteAsync("""
            CREATE OR REPLACE FUNCTION public.aurora_sync_credentials() RETURNS TABLE(id uuid,checkpoint text,credentials text,source_name text)
            LANGUAGE sql AS $$ SELECT NULL::uuid,NULL::text,NULL::text,NULL::text WHERE false $$;
            """);
        var config=runtime.Connections.Single();
        check(await app.Services.GetRequiredService<IIntegrationConnectionCatalog>().Validate("freightops",config.CredentialReference,default)==config.SourceIdentity,"actual host catalog resolves tenant-scoped machine configuration");
        using var http=new HttpClient{BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
        var anonymous=await http.GetAsync("/api/v1/synchronization/connections");check(anonymous.StatusCode==HttpStatusCode.Unauthorized,"host connection listing requires authentication");
        var publishUrl=$"/api/v1/aurora/manifests/{Guid.NewGuid()}/freightops";
        check((await http.PostAsJsonAsync(publishUrl,new{Revision=0})).StatusCode==HttpStatusCode.Unauthorized,"manifest publishing requires authentication");
        http.DefaultRequestHeaders.Add("X-Fixture-Role","aurora:User");
        check((await http.PostAsJsonAsync(publishUrl,new{Revision=0})).StatusCode==HttpStatusCode.Forbidden,"manifest publishing requires dispatcher or administrator role");
        http.DefaultRequestHeaders.Remove("X-Fixture-Role");
        http.DefaultRequestHeaders.Add("X-Fixture-Role","aurora:Admin");
        check((await http.PostAsJsonAsync(publishUrl,new{Revision=0})).StatusCode==HttpStatusCode.BadRequest,"manifest publishing remains disabled for an unconfigured tenant");
        var status=await http.GetAsync("/api/v1/synchronization/connections/status");
        var body=await status.Content.ReadAsStringAsync();
        check(status.IsSuccessStatusCode&&body.Contains("freightops")&&body.Contains("runtime_enabled")&&!body.Contains(config.AccessToken),"host per-provider status exposes runtime state without secrets");
        var connections=await http.GetFromJsonAsync<JsonArray>("/api/v1/synchronization/connections");
        check(connections!.Count==2,"host lists both provider connections while preserving legacy routes");
        long version;
        await using(var session=targetStore.LightweightSession())version=(await session.Events.FetchStreamStateAsync("EXISTING"))!.Version;
        var data=new JsonObject{["Id"]="EXISTING",["TerminalCode"]="TEST",["Lykes"]=new JsonObject{["Version"]=2,["WeightUnit"]="lb",["VolumeUnit"]="ft3",["Lines"]=new JsonArray()}};
        var request=new FreightOpsEnrollmentRequest(Guid.NewGuid(),"EXISTING",version.ToString(),data);
        http.DefaultRequestHeaders.Remove("X-Fixture-Role");http.DefaultRequestHeaders.Add("X-Fixture-Role","aurora:PowerUser");
        check((await http.PostAsJsonAsync("/api/v1/synchronization/freightops/enroll",request)).StatusCode==HttpStatusCode.Forbidden,"enrollment requires administrator role");
        http.DefaultRequestHeaders.Remove("X-Fixture-Role");http.DefaultRequestHeaders.Add("X-Fixture-Role","aurora:Admin");
        var malformed=(JsonObject)data.DeepClone();malformed["Id"]=123;
        check((await http.PostAsJsonAsync("/api/v1/synchronization/freightops/enroll",request with{Data=malformed})).StatusCode==HttpStatusCode.BadRequest,"admin route rejects malformed identity before target access");
        runtime.Enabled=false;
        check((await http.PostAsJsonAsync("/api/v1/synchronization/freightops/enroll",request)).StatusCode==HttpStatusCode.BadRequest,"disabled host refuses enrollment writes");runtime.Enabled=true;
        var first=await http.PostAsJsonAsync("/api/v1/synchronization/freightops/enroll",request);
        var second=await http.PostAsJsonAsync("/api/v1/synchronization/freightops/enroll",request);
        check(first.IsSuccessStatusCode&&second.IsSuccessStatusCode&&await first.Content.ReadAsStringAsync()==await second.Content.ReadAsStringAsync(),"actual admin enrollment route forwards exact replay-safe request");
        var different=request with{ExpectedRevision="999999"};
        check((await http.PostAsJsonAsync("/api/v1/synchronization/freightops/enroll",different)).StatusCode==HttpStatusCode.Conflict,"admin route surfaces enrollment identity/version conflicts");
        await app.StopAsync();
    }
    sealed class FixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role=Request.Headers["X-Fixture-Role"].ToString();
            if(role.Length==0)return Task.FromResult(AuthenticateResult.NoResult());
            var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"fixture"),new Claim(ClaimTypes.Role,role)],Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user,Scheme.Name)));
        }
    }
}
