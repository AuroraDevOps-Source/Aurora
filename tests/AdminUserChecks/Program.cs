using Microsoft.Extensions.Configuration;
using Aurora.Integrations.Nova;
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
var fb=new FbConnectionStringBuilder{DataSource="192.0.2.10",Database=@"D:\test\operations.fdb",UserID="test",Password="test-database-password"};
var tenantId=Guid.Parse(config["FirebirdRouting:TenantId"].GetString()!);
await using var ds=NpgsqlDataSource.Create(pg.ConnectionString);
await using var raw=await ds.OpenConnectionAsync();
var adminId=await raw.QuerySingleAsync<Guid>("SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"UserName\"='bbatts'");
var identity=new TestTenant(tenantId,adminId);
var services=new ServiceCollection();services.AddLogging();services.AddSingleton<ITenantContext>(identity);services.AddSingleton(ds);
services.AddScoped<NpgsqlConnectionFactory>();services.AddScoped<TenantConnectionInterceptor>();
services.AddDbContext<AuroraDbContext>((sp,o)=>{o.UseNpgsql(ds);o.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());});
services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
services.AddIdentityCore<ApplicationUser>(o=>{LoginPasswordPolicy.Configure(o.Password);o.User.RequireUniqueEmail=true;}).AddRoles<ApplicationRole>().AddEntityFrameworkStores<AuroraDbContext>().AddDefaultTokenProviders();
services.Configure<FirebirdRoutingOptions>(o=>{o.Enabled=true;o.TenantId=tenantId;o.ConnectionString=fb.ConnectionString;o.SourceKey=config["FirebirdRouting:SourceKey"].GetString()!;});
services.AddScoped<NovaUserConnections>();services.AddScoped<AdminUsers>();
await using var provider=services.BuildServiceProvider();
int checks=0;
void Check(bool result,string name){if(!result)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
async Task Reject(Func<Task> action,string name){try{await action();}catch(Exception ex) when(ex is FormatException or KeyNotFoundException){Check(true,name);return;}throw new Exception(name);}
async Task<T> Use<T>(Func<AdminUsers,Task<T>> action){await using var scope=provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<AdminUsers>());}
async Task Execute(Func<AdminUsers,Task> action){await using var scope=provider.CreateAsyncScope();await action(scope.ServiceProvider.GetRequiredService<AdminUsers>());}
var suffix=Guid.NewGuid().ToString("N");var password="1";
var input=new AdminUserEdit("codex-check-"+suffix,"codex-check-"+suffix+"@example.invalid","User",password,"");
var localConfig=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Synchronization:LocalWorkspace","true"}}).Build();
Guid? created=null;
try
{
    created=await Use(s=>s.Save(null,input,default));
    var row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    Check(row.Level=="User","create User without a database assignment");
    await using(var scope=provider.CreateAsyncScope())
    {
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var account=(await manager.FindByIdAsync(created.Value.ToString()))!;
        Check(account.InvitationPending && account.PasswordHash is null,"pending login has no usable password hash");
        var token=await FirstLogin.Token(manager,account);
        Check(!(await FirstLogin.Complete(manager,account,FirstLogin.TemporaryPassword,"invalid",password)).Succeeded,"invitation token required");
        Check(!(await FirstLogin.Complete(manager,account,"wrong",token,password)).Succeeded,"temporary password required");
        Check(!(await FirstLogin.Complete(manager,account,FirstLogin.TemporaryPassword,token,FirstLogin.TemporaryPassword)).Succeeded,"must replace temporary password");
        Check((await FirstLogin.Complete(manager,account,FirstLogin.TemporaryPassword,token,password)).Succeeded && await manager.CheckPasswordAsync(account,password),"first sign-in sets personal password");
        Check(!(await FirstLogin.Complete(manager,account,FirstLogin.TemporaryPassword,token,"new")).Succeeded,"invitation cannot be reused");
        var factory=scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>();await using var connection=await factory.OpenConnectionAsync();
        Check(!JsonSerializer.Serialize(row).Contains("DatabaseId"),"user DTO has no database assignment");
    }
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    input=input with{Revision=row.Revision,Level="Power User",LoginPassword=""};
    await Use(s=>s.Save(created,input,default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    Check(row.Level=="Power User","edit user access level");
    await Reject(()=>Use(s=>s.Save(created,input,default)),"stale edits rejected");
    await Use(s=>s.Save(created,input with{Revision=row.Revision,LoginPassword="abc"},default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    await using(var scope=provider.CreateAsyncScope())
    {
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Check(await manager.CheckPasswordAsync((await manager.FindByIdAsync(created.Value.ToString()))!,"abc"),"password reset accepts short lowercase password without digits or symbols");
    }
    await Use(s=>s.Save(created,input with{Revision=row.Revision},default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    var admin=(await Use(s=>s.List(default))).Single(r=>r.Id==adminId);
    await Reject(()=>Execute(s=>s.Delete(adminId,admin.Revision,default)),"cannot delete own administrator login");
    // Simulate a stale Administrator token held by a current Power User.
    identity.User=created.Value;
    await using(var scope=provider.CreateAsyncScope())
    {
        var context=new DefaultHttpContext();context.Request.Path="/api/v1/aurora/firebird";
        context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,created.ToString()!),new Claim(ClaimTypes.Role,"aurora:Admin")},"test"));
        var called=false;var middleware=new UserAccessMiddleware(c=>{called=true;Check(!c.User.IsInRole("aurora:Admin") && c.User.IsInRole("aurora:PowerUser"),"stale administrator claim removed before authorization");return Task.CompletedTask;});
        await middleware.InvokeAsync(context,scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>(),identity,scope.ServiceProvider.GetRequiredService<NovaUserConnections>(),localConfig);
        Check(called,"active member proceeds to authorization");
        var factory=scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>();await using var connection=await factory.OpenConnectionAsync();
        Check(await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings")==0,"non-admin cannot read tenant credentials");
        Check(await connection.ExecuteAsync("UPDATE tenant_settings SET revision=revision+1")==0,"non-admin cannot modify settings");
        Check(await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM user_tenant WHERE tenant_id=@tenantId",new{tenantId})==1,"non-administrator cannot read other users' membership settings");
    }
    identity.User=adminId;
    input=input with{Revision=row.Revision,Level="Administrator"};await Use(s=>s.Save(created,input,default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);Check(row.Level=="Administrator","Administrator level saved");
    identity.Company=Guid.NewGuid();
    await Reject(()=>Use(s=>s.Save(created,input with{Revision=row.Revision},default)),"other company cannot edit user");identity.Company=tenantId;
    await Execute(s=>s.Delete(created.Value,row.Revision,default));
    Check(!(await Use(s=>s.List(default))).Any(r=>r.Id==created),"delete removes user");
    identity.User=created.Value;
    await using(var scope=provider.CreateAsyncScope())
    {
        var context=new DefaultHttpContext();context.Request.Path="/api/v1/aurora/firebird";context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"aurora:Admin")},"test"));
        var called=false;await new UserAccessMiddleware(c=>{called=true;return Task.CompletedTask;}).InvokeAsync(context,scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>(),identity,scope.ServiceProvider.GetRequiredService<NovaUserConnections>(),localConfig);
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
