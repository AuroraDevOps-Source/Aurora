using Aurora.Api.Features.Auth;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.Nova;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text.Json;

var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG")!))!;
var cs=new NpgsqlConnectionStringBuilder(config["ConnectionStrings:Aurora"].GetString());
if(cs.Host!="127.0.0.1" || cs.Port!=55432 || cs.Database!="aurora_test")throw new Exception("Local test database required.");
var credentials=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("deploy/private/local-test/credentials.json"))!;
var ownerCs=new NpgsqlConnectionStringBuilder(cs.ConnectionString){Username="postgres",Password=credentials["postgres"]};
await using var owner=new NpgsqlConnection(ownerCs.ConnectionString);await owner.OpenAsync();
var admin=await owner.QuerySingleAsync<Guid>("""SELECT "Id" FROM "AspNetUsers" WHERE "UserName"='bbatts'""");
var a=Guid.NewGuid();var b=Guid.NewGuid();
await using var ds=NpgsqlDataSource.Create(cs.ConnectionString);
var context=new Context(a,admin);
var factory=new NpgsqlConnectionFactory(ds,context);
var services=new ServiceCollection();services.AddOptions();services.Configure<FirebirdRoutingOptions>(_=>{});
using var provider=services.BuildServiceProvider();
var protection=new EphemeralDataProtectionProvider();
var nova=new NovaUserConnections(protection,provider.GetRequiredService<IOptionsMonitor<FirebirdRoutingOptions>>());
var service=new TenantSettingsService(factory,context,nova,protection);
int count=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
async Task Reject(Func<Task> action,string label){try{await action();}catch(FormatException){Check(true,label);return;}throw new Exception(label);}
try
{
 foreach(var id in new[]{a,b})await owner.ExecuteAsync("""
 INSERT INTO tenant(id,name,slug,is_active,created_utc) VALUES(@id,'Settings check',@slug,true,now());
 INSERT INTO user_tenant(tenant_id,user_id,is_default) VALUES(@id,@admin,false);
 INSERT INTO tenant_product(tenant_id,product_code,instance_url,is_active) VALUES(@id,'freightops','https://old.example.invalid/',true);
 INSERT INTO "AspNetUserRoles"("TenantId","UserId","RoleId") SELECT @id,@admin,"Id" FROM "AspNetRoles" WHERE "Name"='aurora:Admin';
 """,new{id,admin,slug="settings-check-"+id.ToString("N")});
 var input=await service.Read(default);
 input.Nova=new(){Host="192.0.2.1",Database="test.fdb",UserName="test",Password="nova-test-secret"};
 input.FreightOps=new(){ApiUrl="https://freight.example.invalid/",UserName="test",Password="freight-test-secret"};
 input.DispatchTrack=new(){ApiUrl="https://dispatch.example.invalid/orders",ApiCode="dispatch-code",ApiKey="dispatch-key"};
 input.Zenventory=new(){BaseUrl="https://zen.example.invalid/",ApiKey="zen-key",ApiSecret="zen-secret",LegacyBaseUrl="https://legacy.example.invalid/",SecureKey="zen-secure-key"};
 await service.Save(input,default);
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@a AND product_code='freightops'",new{a})==input.FreightOps.ApiUrl,"launcher uses saved FreightOps connection URL");
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@b AND product_code='freightops'",new{b})=="https://old.example.invalid/","saving launcher URL does not change another tenant");
 var saved=await service.Read(default);
 Check(saved.Revision==1 && saved.Nova.HasPassword && saved.FreightOps.HasPassword && saved.DispatchTrack.HasApiCode && saved.DispatchTrack.HasApiKey && saved.Zenventory.HasApiSecret && saved.Zenventory.HasSecureKey,"all four connections saved with configured flags");
 Check(saved.Nova.Password is null && saved.FreightOps.Password is null && saved.DispatchTrack.ApiKey is null && saved.DispatchTrack.ApiCode is null && saved.Zenventory.ApiKey is null && saved.Zenventory.ApiSecret is null && saved.Zenventory.SecureKey is null,"read response excludes every secret");
 var stored=await owner.QuerySingleAsync<string>("SELECT row_to_json(s)::text FROM tenant_settings s WHERE tenant_id=@a",new{a});
 Check(!new[]{"nova-test-secret","freight-test-secret","dispatch-code","dispatch-key","zen-key","zen-secret","zen-secure-key"}.Any(stored.Contains),"all credentials encrypted at rest");
 await service.Save(saved,default);
 var again=await service.Read(default);Check(again.Revision==2 && again.Zenventory.HasApiKey,"blank fields retain saved secrets");
 await Reject(()=>service.Save(saved,default),"stale edit rejected");
 var moved=await service.Read(default);moved.FreightOps.ApiUrl="https://new.example.invalid/";moved.FreightOps.Password="replacement-secret";
 await service.Save(moved,default);
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@a AND product_code='freightops'",new{a})==moved.FreightOps.ApiUrl,"changing Connections also changes launcher destination");
 var changed=await service.Read(default);changed.DispatchTrack.ApiUrl="https://other.example.invalid/";
 await Reject(()=>service.Save(changed,default),"changed destination requires fresh credentials");
 changed=await service.Read(default);changed.Nova.Host="192.0.2.2";changed.Nova.Password="new-secret";
 await Reject(()=>service.Save(changed,default),"source swap cannot mix synchronized data");
 context.Company=b;var other=await service.Read(default);
 Check(!other.Nova.HasPassword && !other.DispatchTrack.HasApiKey,"second tenant sees no first-tenant connections");
 await using(var db=await factory.OpenConnectionAsync())Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings WHERE tenant_id=@a",new{a})==0,"database row security hides another tenant");
 context.Company=a;context.User=Guid.NewGuid();
 await using(var db=await factory.OpenConnectionAsync())
 {
  Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings")==0,"non-admin cannot read settings");
  Check(await db.ExecuteAsync("UPDATE tenant_settings SET revision=revision+1")==0,"non-admin cannot change settings");
  Check(await db.ExecuteScalarAsync<string?>("SELECT public.aurora_tenant_nova_connection()") is null,"nonmember cannot resolve tenant Nova credentials");
 }
 context.User=admin;
 await using(var db=await factory.OpenConnectionAsync())
 {
  var json=await db.ExecuteScalarAsync<string>("SELECT public.aurora_tenant_nova_connection()");
  Check(nova.Resolve(nova.Read(json),a).Enabled,"tenant connection available without per-user database assignment");
 }
 Check(await owner.ExecuteScalarAsync<bool>("SELECT NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_name='user_tenant' AND column_name='nova_database_id')"),"old user database assignment column removed");
}
finally
{
 foreach(var id in new[]{a,b})await owner.ExecuteAsync("""
 DELETE FROM aurora_sync_connection WHERE tenant_id=@id;
 DELETE FROM tenant_settings WHERE tenant_id=@id;
 DELETE FROM "AspNetUserRoles" WHERE "TenantId"=@id;
 DELETE FROM user_tenant WHERE tenant_id=@id;
 DELETE FROM tenant_product WHERE tenant_id=@id;
 DELETE FROM tenant WHERE id=@id;
 """,new{id});
}
Console.WriteLine($"{count} tenant settings checks passed; fixtures removed.");
sealed class Context(Guid company,Guid user):ITenantContext
{
 public Guid Company=company,User=user;
 public Guid? TenantIdOrNull=>Company;
 public Guid? UserIdOrNull=>User;
}
