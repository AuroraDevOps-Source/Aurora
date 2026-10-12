using System.Security.Claims;
using System.Text.Json;
using Aurora.Api.Features.Auth;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

var path=Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG")??throw new Exception("Set local test config path.");
var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(path))!;
var pg=new NpgsqlConnectionStringBuilder(config["ConnectionStrings:Aurora"].GetString());
if(pg.Host!="127.0.0.1" || pg.Port!=55432 || pg.Database!="aurora_test")throw new Exception("Unexpected database.");
var fb=new FbConnectionStringBuilder(config["FirebirdRouting:ConnectionString"].GetString());
if(fb.DataSource!="172.23.0.5" || !fb.Database.Equals(@"D:\db\wayl_test\operations.fdb",StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected Nova database.");
var tenantId=Guid.Parse(config["FirebirdRouting:TenantId"].GetString()!);
await using var ds=NpgsqlDataSource.Create(pg.ConnectionString);
await using var raw=await ds.OpenConnectionAsync();
var adminId=await raw.QuerySingleAsync<Guid>("SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"UserName\"='bbatts'");
var identity=new TestTenant(tenantId,adminId);
var services=new ServiceCollection();services.AddLogging();services.AddSingleton<ITenantContext>(identity);services.AddSingleton(ds);
services.AddScoped<NpgsqlConnectionFactory>();services.AddScoped<TenantConnectionInterceptor>();
services.AddDbContext<AuroraDbContext>((sp,o)=>{o.UseNpgsql(ds);o.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());});
services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
services.AddIdentityCore<ApplicationUser>(o=>{o.Password.RequiredLength=12;o.User.RequireUniqueEmail=true;}).AddRoles<ApplicationRole>().AddEntityFrameworkStores<AuroraDbContext>().AddDefaultTokenProviders();
services.Configure<FirebirdRoutingOptions>(o=>{o.Enabled=true;o.TenantId=tenantId;o.ConnectionString=fb.ConnectionString;o.SourceKey=config["FirebirdRouting:SourceKey"].GetString()!;});
services.AddScoped<NovaUserConnections>();services.AddScoped<AdminUsers>();
await using var provider=services.BuildServiceProvider();
int checks=0;
void Check(bool result,string name){if(!result)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
async Task Reject(Func<Task> action,string name){try{await action();}catch(Exception ex) when(ex is FormatException or KeyNotFoundException){Check(true,name);return;}throw new Exception(name);}
async Task<T> Use<T>(Func<AdminUsers,Task<T>> action){await using var scope=provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<AdminUsers>());}
async Task Execute(Func<AdminUsers,Task> action){await using var scope=provider.CreateAsyncScope();await action(scope.ServiceProvider.GetRequiredService<AdminUsers>());}
var suffix=Guid.NewGuid().ToString("N");var password="Aa1!"+Guid.NewGuid().ToString("N");
var input=new AdminUserEdit("codex-check-"+suffix,"codex-check-"+suffix+"@example.invalid","User",password,"",fb.DataSource,fb.Port,fb.Database,fb.UserID,fb.Password);
Guid? created=null;
try
{
    created=await Use(s=>s.Save(null,input,default));
    var row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    Check(row.Level=="User" && row.HasNovaPassword,"create User with Nova settings");
    await using(var scope=provider.CreateAsyncScope())
    {
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Check(await manager.CheckPasswordAsync((await manager.FindByIdAsync(created.Value.ToString()))!,password),"new login password works");
        var factory=scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>();await using var connection=await factory.OpenConnectionAsync();
        var json=await connection.QuerySingleAsync<string>("SELECT nova_connection::text FROM user_tenant WHERE tenant_id=@tenantId AND user_id=@created",new{tenantId,created});
        var settings=scope.ServiceProvider.GetRequiredService<NovaUserConnections>();var stored=settings.Read(json)!;
        Check(stored.ProtectedPassword!=fb.Password && !json.Contains("\"Password\":"),"database password encrypted at rest");
        Check(!JsonSerializer.Serialize(row).Contains(stored.ProtectedPassword),"API user DTO never exposes encrypted password");
        Check(new FbConnectionStringBuilder(settings.Resolve(stored,tenantId).ConnectionString).Password==fb.Password,"saved credential decrypts for server connection");
        Check(stored.SourceKey==settings.Default.SourceKey,"same Nova database keeps stable source identity across users");
        var alternative=settings.Build(input with{NovaHost="192.0.2.10",NovaDatabase=@"D:\other\operations.fdb"},stored);
        var resolved=new FbConnectionStringBuilder(settings.Resolve(alternative,tenantId).ConnectionString);
        Check(resolved.DataSource=="192.0.2.10" && alternative.SourceKey!=stored.SourceKey,"different user database resolves distinct connection and source");
    }
    input=input with{Revision=row.Revision,Level="Power User",LoginPassword="",NovaPassword=""};
    await Use(s=>s.Save(created,input,default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    Check(row.Level=="Power User" && row.HasNovaPassword,"edit level preserves blank Nova password");
    await Reject(()=>Use(s=>s.Save(created,input,default)),"stale edits rejected");
    await Execute(s=>s.Test(created,input with{Revision=row.Revision},default));Check(true,"saved credentials connect to Nova (read only)");
    var admin=(await Use(s=>s.List(default))).Single(r=>r.Id==adminId);
    await Reject(()=>Execute(s=>s.Delete(adminId,admin.Revision,default)),"cannot delete own administrator login");
    // Simulate a stale Administrator token held by a current Power User.
    identity.User=created.Value;
    await using(var scope=provider.CreateAsyncScope())
    {
        var context=new DefaultHttpContext();context.Request.Path="/api/v1/admin/users";
        context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,created.ToString()!),new Claim(ClaimTypes.Role,"aurora:Admin")},"test"));
        var called=false;var middleware=new UserAccessMiddleware(c=>{called=true;Check(!c.User.IsInRole("aurora:Admin") && c.User.IsInRole("aurora:PowerUser"),"stale administrator claim removed before authorization");return Task.CompletedTask;});
        await middleware.InvokeAsync(context,scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>(),identity,scope.ServiceProvider.GetRequiredService<NovaUserConnections>());
        Check(called,"active member proceeds to authorization");
        var factory=scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>();await using var connection=await factory.OpenConnectionAsync();
        Check(await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM user_tenant WHERE tenant_id=@tenantId",new{tenantId})==1,"non-administrator cannot read other users' membership settings");
    }
    identity.User=adminId;
    input=input with{Revision=row.Revision,Level="Administrator"};await Use(s=>s.Save(created,input,default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);Check(row.Level=="Administrator","Administrator level saved");
    identity.Company=Guid.NewGuid();await Reject(()=>Use(s=>s.Save(created,input with{Revision=row.Revision},default)),"other company cannot edit user");identity.Company=tenantId;
    await Execute(s=>s.Delete(created.Value,row.Revision,default));
    Check(!(await Use(s=>s.List(default))).Any(r=>r.Id==created),"delete removes user");
    identity.User=created.Value;
    await using(var scope=provider.CreateAsyncScope())
    {
        var context=new DefaultHttpContext();context.Request.Path="/api/v1/aurora/firebird";context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"aurora:Admin")},"test"));
        var called=false;await new UserAccessMiddleware(c=>{called=true;return Task.CompletedTask;}).InvokeAsync(context,scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>(),identity,scope.ServiceProvider.GetRequiredService<NovaUserConnections>());
        Check(!called && context.Response.StatusCode==401,"deleted login cannot use existing API token");
    }
    created=null;
}
finally
{
    identity.User=adminId;identity.Company=tenantId;
    if(created is {} cleanup){var row=(await Use(s=>s.List(default))).SingleOrDefault(r=>r.Id==cleanup);if(row is not null)await Execute(s=>s.Delete(cleanup,row.Revision,default));}
}
Console.WriteLine($"{checks} user administration checks passed. Temporary user removed; Nova data unchanged.");
sealed class TestTenant(Guid company,Guid user):ITenantContext {public Guid Company=company;public Guid User=user;public Guid? TenantIdOrNull=>Company;public Guid? UserIdOrNull=>User;}
