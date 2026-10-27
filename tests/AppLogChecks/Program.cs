using Aurora.Integrations.Nova;
using System.Security.Claims;
using System.Text.Json;
using Aurora.Api.Features.Auth;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var builder=WebApplication.CreateBuilder();
builder.Services.AddAuthorization();builder.Services.AddScoped<NpgsqlConnectionFactory>();builder.Services.AddScoped<ITenantContext>(_=>new LogTenant(Guid.NewGuid(),Guid.NewGuid()));
await using var app=builder.Build();app.MapAppLogEndpoints();
var endpoint=((IEndpointRouteBuilder)app).DataSources.SelectMany(d=>d.Endpoints).OfType<RouteEndpoint>().Single(e=>e.RoutePattern.RawText=="/api/v1/admin/applog");
var policy=(await AuthorizationPolicy.CombineAsync(app.Services.GetRequiredService<IAuthorizationPolicyProvider>(),endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()))!;
foreach(var role in new[]{"aurora:Admin","aurora:PowerUser","aurora:User","other:Admin",""})
{
 var user=new ClaimsPrincipal(new ClaimsIdentity(role==""?[]:[new Claim(ClaimTypes.Role,role)],role==""?null:"test"));
 Check((await app.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user,null,policy)).Succeeded==(role is "aurora:Admin" or "aurora:PowerUser"),"viewer access: "+role);
}
Check(!AppLog.SafeDetail("Password=abc; pwd=def token=ghi secret=jkl").Contains("abc"),"credential redaction");
Check(AppLog.Describe("/api/auth/login","POST",200)=="Logged in","plain login action");
Check(AppLog.Describe("/api/v1/aurora/firebird/records/orders/10","PUT",200)=="Edited order","plain order edit");
Check(AppLog.Describe("/api/v1/routing/sessions","POST",200)=="Ran optimization","plain optimization action");
Check(AppLog.Describe("/api/v1/aurora/firebird/records/fleet/10","DELETE",200)=="Deleted truck","plain delete action");
Check(AppLog.Describe("/api/v1/aurora/firebird/records/orders","POST",400)=="Added order — failed","failed actions not shown as successful");
var background=new DefaultHttpContext();background.Request.Method="GET";background.Request.Path="/api/v1/aurora/drafts";
Check(!AppLog.IsUserAction(background),"background reads omitted");
background.Request.Method="POST";background.Request.Path="/api/v1/aurora/firebird/refresh";
Check(!AppLog.IsUserAction(background),"background refresh omitted");
var clearEndpoint=((IEndpointRouteBuilder)app).DataSources.SelectMany(d=>d.Endpoints).OfType<RouteEndpoint>().Single(e=>e.RoutePattern.RawText=="/api/v1/admin/applog/clear");
var orderDocument=new NovaRecordDocument(9001,"Order", "revision",[new("header","Order",[new("PBNUM","PRO number","integer",20,true,"123456")])]);
Check(AppLog.BusinessReferences("/firebird/records/orders/9001",orderDocument)=="Order/PRO 123456","order log uses PRO number instead of database key");
var truckDocument=new NovaRecordDocument(9002,"Vehicle", "revision",[new("header","Truck",[new("EH_UNIT_ID","Unit number","text",14,true,"TRK-42")])]);
Check(AppLog.BusinessReferences("/firebird/records/fleet/9002",truckDocument)=="Truck TRK-42","truck log uses unit number");
Check(AppLog.BusinessReferences("/firebird/manifests",11455L)=="Manifest 11455","manual manifest number captured");
var manifests=new[]{new SavedManifestDto(Guid.NewGuid(),Guid.NewGuid(),"truck",DateTimeOffset.UtcNow,null!){Number="11455"},new SavedManifestDto(Guid.NewGuid(),Guid.NewGuid(),"truck2",DateTimeOffset.UtcNow,null!){Number="11456"}};
Check(AppLog.BusinessReferences("/drafts/abc/finish",manifests)=="Manifest 11455; Manifest 11456","all optimized manifest numbers captured");
Check(AppLog.BusinessReferences("/drafts/abc/finish",new[]{manifests[0] with{Number=null}})=="","missing business number never replaced by internal GUID");
var dispatch=new NovaDispatchDocument(11455,"revision","Dispatched",null,"MHT",1,2,2,"TRK-42",false,true,null,[],[],[]);
Check(AppLog.BusinessReferences("/firebird/manifests/11455/dispatch",dispatch)=="Manifest 11455","dispatch references the customer-facing manifest number");
var legacy=new AppLogEntry(Guid.NewGuid(),DateTime.UtcNow,"user","Information","Dispatched manifest","/api/v1/aurora/firebird/manifests/{id:long}/dispatch",200,"HTTP 200 · Record 11455","trace",1);
Check(AppLog.ForDisplay(legacy).References=="Manifest 11455","existing dispatch logs expose saved manifest numbers");
Check(AppLog.ForDisplay(legacy with{Detail="HTTP 200 Â· Record 11455"}).References=="Manifest 11455","legacy encoding does not hide manifest numbers");
Check(AppLog.ForDisplay(legacy with{Resource="/firebird/records/orders/{id:long}"}).References=="","legacy order database key is not presented as PRO number");
Check(AppLog.ForDisplay(legacy with{Detail="HTTP 200 · References: Manifest 11455; Manifest 11456"}).References=="Manifest 11455; Manifest 11456","business references survive display roundtrip");
var filter=new AppLogResultFilter();
var editContext=new DefaultHttpContext();editContext.Request.Method="PUT";editContext.Request.Path="/firebird/records/orders/9001";
await filter.InvokeAsync(EndpointFilterInvocationContext.Create(editContext),_=>ValueTask.FromResult<object?>(Results.Ok(orderDocument)));
Check(editContext.Items[AppLog.ReferencesKey] as string=="Order/PRO 123456","result filter captures edited order number");
var failedContext=new DefaultHttpContext();failedContext.Request.Method="POST";failedContext.Request.Path="/firebird/manifests";
await filter.InvokeAsync(EndpointFilterInvocationContext.Create(failedContext),_=>ValueTask.FromResult<object?>(Results.BadRequest(new ApiErrorDto("Rejected"))));
Check(!failedContext.Items.ContainsKey(AppLog.ReferencesKey),"failed creation does not claim a manifest number");
Check(clearEndpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count>0 || clearEndpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Count>0,"clear endpoint requires authorization");
if(Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG") is null){Console.WriteLine($"{checks} application log checks passed; database checks require configured fixture.");return;}
var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG")!))!;
var settings=new NpgsqlConnectionStringBuilder(config["ConnectionStrings:Aurora"].GetString());
if(settings.Host!="127.0.0.1" || settings.Port!=55432 || settings.Database!="aurora_test")throw new Exception("Local test DB only.");
await using var ds=NpgsqlDataSource.Create(settings.ConnectionString);
await using var lookup=await ds.OpenConnectionAsync();
var admin=await lookup.QuerySingleAsync<Guid>("SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"UserName\"='bbatts'");
var tenantId=Guid.Parse(config["FirebirdRouting:TenantId"].GetString()!);
var tenant=new LogTenant(tenantId,admin);
await using var db=await new NpgsqlConnectionFactory(ds,tenant).OpenConnectionAsync();
await using(var tx=await db.BeginTransactionAsync())
{
 var id=Guid.NewGuid();
 await db.ExecuteAsync("INSERT INTO applog(id,tenant_id,user_id,user_name,level,action,resource,status_code,detail,trace_id,duration_ms) VALUES(@id,@tenantId,@admin,'Log check','Information','Test','Test',200,'Test','Test',0)",new{id,tenantId,admin},tx);
 Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM applog WHERE id=@id",new{id},tx)==1,"admin sees own tenant log");
 await db.ExecuteAsync("DELETE FROM \"AspNetUserRoles\" WHERE \"UserId\"=@admin AND \"TenantId\"=@tenantId",new{admin,tenantId},tx);
 Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM applog WHERE id=@id",new{id},tx)==0,"database blocks users without elevated role");
 await db.ExecuteAsync("INSERT INTO \"AspNetUserRoles\" (\"UserId\",\"TenantId\",\"RoleId\") SELECT @admin,@tenantId,\"Id\" FROM \"AspNetRoles\" WHERE \"Name\"='aurora:PowerUser'",new{admin,tenantId},tx);
 Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM applog WHERE id=@id",new{id},tx)==1,"Power User can read log");
 await db.ExecuteAsync("SELECT set_config('app.tenant_id',@other,true)",new{other=Guid.NewGuid().ToString()},tx);
 Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM applog WHERE id=@id",new{id},tx)==0,"other tenant cannot read log");
 await tx.RollbackAsync();
}
Check(!await db.ExecuteScalarAsync<bool>("SELECT has_table_privilege(current_user,'applog','UPDATE') OR has_table_privilege(current_user,'applog','DELETE')"),"application cannot alter or delete logs");
var service=new AppLog(ds,tenant,NullLogger<AppLog>.Instance);
DefaultHttpContext Context(string path,int status)
{
 var c=new DefaultHttpContext();c.Request.Path=path;c.Request.Method="POST";c.Response.StatusCode=status;c.TraceIdentifier="applog-check-"+Guid.NewGuid();
 c.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name,"bbatts"),new Claim(ClaimTypes.NameIdentifier,admin.ToString()),new Claim("tenant_id",tenantId.ToString())],"test"));return c;
}
var success=Context("/api/auth/login",200);success.Items[AppLog.ActorKey]=new AppLog.Actor(admin,"bbatts");
await service.Write(success,null,5);
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM applog WHERE trace_id=@trace AND action='Logged in' AND user_id=@admin",new{trace=success.TraceIdentifier,admin})==1,"successful sign-in attributed");
var failed=Context("/api/auth/login",401);await service.Write(failed,null,5);
Check(await db.ExecuteScalarAsync<string>("SELECT action FROM applog WHERE trace_id=@trace",new{trace=failed.TraceIdentifier})=="Login failed","failed sign-in recorded");
var fault=Context("/api/v1/test",200);
try {await new AppLogMiddleware(_=>throw new InvalidOperationException("Password=never-store-this")).InvokeAsync(fault,service);}catch(InvalidOperationException){}
var detail=await db.QuerySingleAsync<(string Level,string Detail)>("SELECT level,detail FROM applog WHERE trace_id=@trace",new{trace=fault.TraceIdentifier});
Check(detail.Level=="Error" && detail.Detail.Contains("InvalidOperationException") && !detail.Detail.Contains("never-store-this"),"unhandled error recorded without exception secrets");
var view=Context("/api/v1/admin/applog",200);await new AppLogMiddleware(_=>Task.CompletedTask).InvokeAsync(view,service);
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM applog WHERE trace_id=@trace",new{trace=view.TraceIdentifier})==0,"log reads do not create recursive noise");
var created=Context("/api/v1/aurora/drafts/test/finish",200);
created.Items[AppLog.ReferencesKey]=AppLog.BusinessReferences(created.Request.Path.Value!,manifests);
await service.Write(created,null,5);
foreach(var number in new[]{"11455","11456"})
    Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM applog WHERE trace_id=@trace AND strpos(lower(user_name || ' ' || action || ' ' || resource || ' ' || detail || ' ' || trace_id),lower(@number))>0",new{trace=created.TraceIdentifier,number})==1,"saved log searchable by manifest "+number);
var savedDetail=await db.ExecuteScalarAsync<string>("SELECT detail FROM applog WHERE trace_id=@trace",new{trace=created.TraceIdentifier});
Check(AppLog.ForDisplay(legacy with{Detail=savedDetail!}).References=="Manifest 11455; Manifest 11456","persisted business numbers display after reload");
Console.WriteLine($"PASS {checks} application log checks.");
sealed record LogTenant(Guid? TenantIdOrNull,Guid? UserIdOrNull):ITenantContext;
