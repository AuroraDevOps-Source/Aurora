using System.Security.Claims;
using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Aurora.Api.Features.Auth;

public sealed record StoredNovaConnection(string Host,int Port,string Database,string UserName,string ProtectedPassword,string SourceKey);

public sealed class NovaUserConnections(IDataProtectionProvider protection,IOptionsMonitor<FirebirdRoutingOptions> defaults)
{
    public const string ItemsKey="Aurora.UserNovaOptions";
    private IDataProtector Protector=>protection.CreateProtector("Aurora.NovaUserPassword.v1");
    public FirebirdRoutingOptions Default=>defaults.CurrentValue;
    public StoredNovaConnection? Read(string? json)=>json is null?null:JsonSerializer.Deserialize<StoredNovaConnection>(json);
    public StoredNovaConnection? Existing(string? json,Guid tenantId)
    {
        var saved=Read(json);if(saved is not null)return saved;
        if(Default.TenantId!=tenantId || !Default.Enabled || string.IsNullOrWhiteSpace(Default.ConnectionString))return null;
        var connection=new FbConnectionStringBuilder(Default.ConnectionString);
        return new(connection.DataSource,connection.Port,connection.Database,connection.UserID,Protector.Protect(connection.Password),Default.SourceKey);
    }
    public StoredNovaConnection Build(AdminUserEdit input,StoredNovaConnection? existing)
    {
        var host=input.NovaHost.Trim();var database=input.NovaDatabase.Trim();var user=input.NovaUserName.Trim();
        if(host.Length is 0 or >255 || database.Length is 0 or >1000 || user.Length is 0 or >128 || input.NovaPort is <1 or >65535)
            throw new FormatException("Enter a Nova server, port, database path, and username.");
        if(host.Any(char.IsWhiteSpace) || host.Contains('/') || host.Contains('\\'))throw new FormatException("Enter a server IP address or hostname without a URL or path.");
        var same=existing is not null && existing.Host.Equals(host,StringComparison.OrdinalIgnoreCase) && existing.Port==input.NovaPort && existing.Database.Equals(database,StringComparison.OrdinalIgnoreCase);
        var credentialSame=same && existing!.UserName.Equals(user,StringComparison.OrdinalIgnoreCase);
        var password=string.IsNullOrEmpty(input.NovaPassword)?(credentialSame?existing!.ProtectedPassword:null):Protector.Protect(input.NovaPassword);
        if(password is null)throw new FormatException("Enter the Nova password for this connection.");
        // Keep the legacy source identity when editing the current connection; new databases get distinct snapshots.
        var baseline=string.IsNullOrWhiteSpace(Default.ConnectionString)?null:new FbConnectionStringBuilder(Default.ConnectionString);
        var matchesDefault=baseline is not null && baseline.DataSource.Equals(host,StringComparison.OrdinalIgnoreCase) && baseline.Port==input.NovaPort && baseline.Database.Equals(database,StringComparison.OrdinalIgnoreCase);
        var key=same?existing!.SourceKey:matchesDefault?Default.SourceKey:"nova-"+FirebirdRoutingSource.StableId($"{host.ToLowerInvariant()}:{input.NovaPort}:{database.ToLowerInvariant()}").ToString("N");
        return new(host,input.NovaPort,database,user,password,key);
    }
    public FirebirdRoutingOptions Resolve(StoredNovaConnection? saved,Guid tenantId)
    {
        if(saved is null)return new(){Enabled=false};
        var cs=new FbConnectionStringBuilder{DataSource=saved.Host,Port=saved.Port,Database=saved.Database,UserID=saved.UserName,Password=Protector.Unprotect(saved.ProtectedPassword),Charset="UTF8",ConnectionTimeout=10};
        return new(){Enabled=true,TenantId=tenantId,ConnectionString=cs.ConnectionString,SourceKey=saved.SourceKey,ReadyStatus=Default.ReadyStatus,TimeZoneId=Default.TimeZoneId,ServiceSeconds=Default.ServiceSeconds};
    }
}

public sealed class UserAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context,NpgsqlConnectionFactory factory,ITenantContext tenant,NovaUserConnections connections)
    {
        if(context.Request.Path.StartsWithSegments("/api/v1") && context.User.Identity?.IsAuthenticated==true)
        {
            await using var db=await factory.OpenConnectionAsync(context.RequestAborted);
            var membership=await db.QuerySingleOrDefaultAsync<Member>(new CommandDefinition("SELECT nova_connection::text AS Json FROM user_tenant WHERE tenant_id=@TenantId AND user_id=@UserId",new{tenant.TenantId,tenant.UserId},cancellationToken:context.RequestAborted));
            if(membership is null){context.Response.StatusCode=401;return;}
            var roles=(await db.QueryAsync<string>(new CommandDefinition("""
                SELECT r."Name" FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
                WHERE ur."TenantId"=@TenantId AND ur."UserId"=@UserId AND r."Name" LIKE 'aurora:%'
                """,new{tenant.TenantId,tenant.UserId},cancellationToken:context.RequestAborted))).ToArray();
            // Authorization sees current levels, so demotion/deletion takes effect on the next API request.
            foreach(var identity in context.User.Identities)
            {
                foreach(var claim in identity.Claims.Where(c=>c.Type==identity.RoleClaimType && c.Value.StartsWith("aurora:")).ToArray())identity.RemoveClaim(claim);
            }
            if(context.User.Identity is ClaimsIdentity current)foreach(var role in roles)current.AddClaim(new Claim(current.RoleClaimType,role));
            if(context.Request.Path.StartsWithSegments("/api/v1/aurora") || context.Request.Path.StartsWithSegments("/api/v1/routing") || context.Request.Path.StartsWithSegments("/api/v1/tms"))
            {
                try{context.Items[NovaUserConnections.ItemsKey]=connections.Resolve(connections.Existing(membership.Json,tenant.TenantId),tenant.TenantId);}
                catch(System.Security.Cryptography.CryptographicException){context.Response.StatusCode=503;await context.Response.WriteAsJsonAsync(new ApiErrorDto("The saved Nova password could not be opened. Ask an administrator to re-enter it."));return;}
            }
        }
        await next(context);
    }
    private sealed record Member(string? Json);
}
