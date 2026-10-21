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
services.AddScoped<NovaUserConnections>();services.AddScoped<AdminUsers>();services.AddScoped<AdminDatabases>();
await using var provider=services.BuildServiceProvider();
int checks=0;
void Check(bool result,string name){if(!result)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
async Task Reject(Func<Task> action,string name){try{await action();}catch(Exception ex) when(ex is FormatException or KeyNotFoundException){Check(true,name);return;}throw new Exception(name);}
async Task<T> Use<T>(Func<AdminUsers,Task<T>> action){await using var scope=provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<AdminUsers>());}
async Task Execute(Func<AdminUsers,Task> action){await using var scope=provider.CreateAsyncScope();await action(scope.ServiceProvider.GetRequiredService<AdminUsers>());}
var suffix=Guid.NewGuid().ToString("N");var password="1";
async Task<T> Database<T>(Func<AdminDatabases,Task<T>> action){await using var scope=provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<AdminDatabases>());}
async Task DatabaseAction(Func<AdminDatabases,Task> action){await using var scope=provider.CreateAsyncScope();await action(scope.ServiceProvider.GetRequiredService<AdminDatabases>());}
var databaseInput=new AdminDatabaseEdit("check-"+suffix,0,fb.DataSource,fb.Port,fb.Database,fb.UserID,fb.Password);
var databaseId=await Database(s=>s.Save(null,databaseInput,default));
var otherDatabaseId=await Database(s=>s.Save(null,databaseInput with{Name="other-"+suffix},default));
var input=new AdminUserEdit("codex-check-"+suffix,"codex-check-"+suffix+"@example.invalid","User",password,"",databaseId);
Guid? created=null;
try
{
    await Reject(()=>Use(s=>s.Save(null,input with{DatabaseId=null},default)),"database selection required");
    await Reject(()=>Use(s=>s.Save(null,input with{DatabaseId=Guid.NewGuid()},default)),"unknown database rejected");
    created=await Use(s=>s.Save(null,input,default));
    var row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    Check(row.Level=="User" && row.DatabaseId==databaseId,"create User with selected database");
    await using(var scope=provider.CreateAsyncScope())
    {
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Check(await manager.CheckPasswordAsync((await manager.FindByIdAsync(created.Value.ToString()))!,password),"new login password works");
        var factory=scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>();await using var connection=await factory.OpenConnectionAsync();
        var json=await connection.QuerySingleAsync<string>("SELECT connection::text FROM nova_database WHERE tenant_id=@tenantId AND id=@databaseId",new{tenantId,databaseId});
        var settings=scope.ServiceProvider.GetRequiredService<NovaUserConnections>();var stored=settings.Read(json)!;
        Check(stored.ProtectedPassword!=fb.Password && !json.Contains("\"Password\":"),"database password encrypted at rest");
        Check(!JsonSerializer.Serialize(row).Contains(stored.ProtectedPassword),"API user DTO never exposes encrypted password");
        Check(new FbConnectionStringBuilder(settings.Resolve(stored,tenantId).ConnectionString).Password==fb.Password,"saved credential decrypts for server connection");
        Check(stored.SourceKey==settings.Default.SourceKey,"same Nova database keeps stable source identity across users");
        var alternative=settings.Build(databaseInput with{Host="192.0.2.11",Database=@"D:\other\operations.fdb"},stored);
        var resolved=new FbConnectionStringBuilder(settings.Resolve(alternative,tenantId).ConnectionString);
        Check(resolved.DataSource=="192.0.2.11" && alternative.SourceKey!=stored.SourceKey,"different user database resolves distinct connection and source");
    }
    input=input with{Revision=row.Revision,Level="Power User",LoginPassword=""};
    await Use(s=>s.Save(created,input,default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    Check(row.Level=="Power User" && row.DatabaseId==databaseId,"edit level preserves database selection");
    await Reject(()=>Use(s=>s.Save(created,input,default)),"stale edits rejected");
    var databaseRow=(await Database(s=>s.List(default))).Single(d=>d.Id==databaseId);
    Check(databaseRow.UserCount==1,"database shows assigned user count");
    try{await DatabaseAction(s=>s.Delete(databaseId,databaseRow.Revision,default));throw new Exception("Assigned database was deleted");}catch(PostgresException ex) when(ex.SqlState=="23503"){Check(true,"assigned database deletion rejected");}
    await Database(s=>s.Save(databaseId,databaseInput with{Password="",Name="renamed-"+suffix},default));
    await Reject(()=>Database(s=>s.Save(databaseId,databaseInput,default)),"stale database changes rejected");
    await Use(s=>s.Save(created,input with{Revision=row.Revision,DatabaseId=otherDatabaseId,LoginPassword="abc"},default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);
    Check(row.DatabaseId==otherDatabaseId,"user can switch databases");
    await using(var scope=provider.CreateAsyncScope())
    {
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Check(await manager.CheckPasswordAsync((await manager.FindByIdAsync(created.Value.ToString()))!,"abc"),"password reset accepts short lowercase password without digits or symbols");
    }
    await Use(s=>s.Save(created,input with{Revision=row.Revision,DatabaseId=databaseId},default));
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
        await middleware.InvokeAsync(context,scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>(),identity,scope.ServiceProvider.GetRequiredService<NovaUserConnections>());
        Check(called,"active member proceeds to authorization");
        var selected=(FirebirdRoutingOptions)context.Items[NovaUserConnections.ItemsKey]!;
        Check(new FbConnectionStringBuilder(selected.ConnectionString).DataSource==fb.DataSource,"middleware uses assigned catalog connection");
        var factory=scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>();await using var connection=await factory.OpenConnectionAsync();
        Check(await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM nova_database WHERE tenant_id=@tenantId",new{tenantId})==1,"non-admin can read only assigned connection");
        Check(await connection.ExecuteAsync("UPDATE nova_database SET name='forbidden' WHERE tenant_id=@tenantId AND id=@databaseId",new{tenantId,databaseId})==0,"non-admin cannot modify shared connection");
        Check(await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM user_tenant WHERE tenant_id=@tenantId",new{tenantId})==1,"non-administrator cannot read other users' membership settings");
    }
    identity.User=adminId;
    input=input with{Revision=row.Revision,Level="Administrator"};await Use(s=>s.Save(created,input,default));
    row=(await Use(s=>s.List(default))).Single(r=>r.Id==created);Check(row.Level=="Administrator","Administrator level saved");
    identity.Company=Guid.NewGuid();
    Check((await Database(s=>s.List(default))).Length==0,"database catalog isolated by company");
    await Reject(()=>Database(s=>s.Save(databaseId,databaseInput with{Revision=1},default)),"other company cannot edit database");
    await Reject(()=>Use(s=>s.Save(created,input with{Revision=row.Revision},default)),"other company cannot edit user");identity.Company=tenantId;
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
    foreach(var database in (await Database(s=>s.List(default))).Where(d=>d.Id==databaseId || d.Id==otherDatabaseId))await DatabaseAction(s=>s.Delete(database.Id,database.Revision,default));
}
Console.WriteLine($"{checks} user administration checks passed. Temporary user removed; Nova data unchanged.");
sealed class TestTenant(Guid company,Guid user):ITenantContext {public Guid Company=company;public Guid User=user;public Guid? TenantIdOrNull=>Company;public Guid? UserIdOrNull=>User;}
