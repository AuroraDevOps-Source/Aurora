using Aurora.Api.Features.Auth;
using Aurora.Api.Features.Routing;
using Microsoft.Extensions.Logging.Abstractions;
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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

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
var history=new OrderImportHistoryStore(factory,context,NullLogger<OrderImportHistoryStore>.Instance);
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
 var globalRules=input.GlobalDocumentSettings!.Instructions;
 Check(input.DocumentExtractionInstructions=="","a new company starts with shared extraction rules");
 const string companyInstructions="Read a parcel label as the shipment reference.";
 input.DocumentExtractionInstructions=companyInstructions;
 Check(input.DriverCostPerHour==0,"new tenant defaults to zero driver cost");
 Check(input.Navigation is {Optimization:true,DataEntry:true,FreightOps:true,Hub:true,Nova:true},"new tenant shows all menu options by default");
 input.CompanyName=" Updated company ";
 input.Navigation=new(){Optimization=false,DataEntry=true,FreightOps=false,Hub=false,Nova=false};
 input.DriverCostPerHour=27.5m;
 input.Nova=new(){Host="192.0.2.1",Database="test.fdb",UserName="test",Password="nova-test-secret"};
 input.FreightOps=new(){ApiUrl="https://freight.example.invalid/",UserName="test",Password="freight-test-secret"};
 input.DispatchTrack=new(){ApiUrl="https://dispatch.example.invalid/orders",ApiCode="dispatch-code",ApiKey="dispatch-key"};
 input.Zenventory=new(){BaseUrl="https://zen.example.invalid/",ApiKey="zen-key",ApiSecret="zen-secret",LegacyBaseUrl="https://legacy.example.invalid/",SecureKey="zen-secure-key"};
 await service.Save(input,default);
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@a AND product_code='freightops'",new{a})==input.FreightOps.ApiUrl,"launcher uses saved FreightOps connection URL");
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@b AND product_code='freightops'",new{b})=="https://old.example.invalid/","saving launcher URL does not change another tenant");
 var saved=await service.Read(default);
 Check(saved.DocumentExtractionInstructions==companyInstructions,"company extraction instructions round-trip");
 Check(saved.CompanyName=="Updated company","company name saved and trimmed");
 Check(saved.Navigation is {Optimization:false,DataEntry:true,FreightOps:false,Hub:false,Nova:false},"menu preferences persist independently");
 Check(!saved.Navigation!.IsVisible("aurora") && saved.Navigation.DataEntry,"Data Entry can remain visible with Optimization hidden");
 Check(await owner.ExecuteScalarAsync<string>("SELECT name FROM tenant WHERE id=@b",new{b})=="Settings check","company rename isolated to current tenant");
 Check(saved.DriverCostPerHour==27.5m,"global hourly driver cost persists");
 await using(var db=await factory.OpenConnectionAsync())Check(await db.ExecuteScalarAsync<decimal>("SELECT public.aurora_driver_cost_per_hour()") == 27.5m,"optimization reads saved hourly cost");
 Check(saved.Revision==1 && saved.Nova.HasPassword && saved.FreightOps.HasPassword && saved.DispatchTrack.HasApiCode && saved.DispatchTrack.HasApiKey && saved.Zenventory.HasApiSecret && saved.Zenventory.HasSecureKey,"all four connections saved with configured flags");
 Check(saved.Nova.Password is null && saved.FreightOps.Password is null && saved.DispatchTrack.ApiKey is null && saved.DispatchTrack.ApiCode is null && saved.Zenventory.ApiKey is null && saved.Zenventory.ApiSecret is null && saved.Zenventory.SecureKey is null,"read response excludes every secret");
 var stored=await owner.QuerySingleAsync<string>("SELECT row_to_json(s)::text FROM tenant_settings s WHERE tenant_id=@a",new{a});
 Check(!new[]{"nova-test-secret","freight-test-secret","dispatch-code","dispatch-key","zen-key","zen-secret","zen-secure-key"}.Any(stored.Contains),"all credentials encrypted at rest");
 await service.Save(saved,default);
 var again=await service.Read(default);Check(again.Revision==2 && again.Zenventory.HasApiKey,"blank fields retain saved secrets");
 await Reject(()=>service.Save(saved,default),"stale edit rejected");
 var badName=await service.Read(default);badName.CompanyName=" ";
 await Reject(()=>service.Save(badName,default),"blank company name rejected");
 badName.CompanyName=new string('x',201);await Reject(()=>service.Save(badName,default),"overlong company name rejected");
 var compatibility=await service.Read(default);compatibility.Navigation=null;compatibility.DocumentExtractionInstructions=null;
 await service.Save(compatibility,default);
 Check((await service.Read(default)).DocumentExtractionInstructions==companyInstructions,"older settings clients preserve company instructions");
 var tooLong=await service.Read(default);tooLong.DocumentExtractionInstructions=new string('a',TenantSettingsDto.MaxDocumentInstructionsLength+1);
 await Reject(()=>service.Save(tooLong,default),"overlong company instructions are rejected");
 Check((await service.Read(default)).Navigation!.Optimization==false,"settings clients omitting menu preferences preserve saved preferences");
 var invalid=await service.Read(default);invalid.DriverCostPerHour=-1;
 await Reject(()=>service.Save(invalid,default),"negative driver cost rejected");
 invalid.DriverCostPerHour=1.00001m;await Reject(()=>service.Save(invalid,default),"excess driver cost precision rejected");
 invalid.DriverCostPerHour=35.25m;await service.Save(invalid,default);
 Check((await service.Read(default)).DriverCostPerHour==35.25m,"administrator can change hourly driver cost");
 var moved=await service.Read(default);moved.FreightOps.ApiUrl="https://new.example.invalid/";moved.FreightOps.Password="replacement-secret";
 await service.Save(moved,default);
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@a AND product_code='freightops'",new{a})==moved.FreightOps.ApiUrl,"changing Connections also changes launcher destination");
 var changed=await service.Read(default);changed.DispatchTrack.ApiUrl="https://other.example.invalid/";
 await Reject(()=>service.Save(changed,default),"changed destination requires fresh credentials");
 changed=await service.Read(default);changed.Nova.Host="192.0.2.2";changed.Nova.Password="new-secret";
 await Reject(()=>service.Save(changed,default),"source swap cannot mix synchronized data");
 context.Company=b;var other=await service.Read(default);
 Check(other.DocumentExtractionInstructions=="","second tenant does not receive the first tenant's instructions");
 other.DocumentExtractionInstructions="Temporary rule";await service.Save(other,default);
 other=await service.Read(default);other.DocumentExtractionInstructions="";await service.Save(other,default);
 Check((await service.Read(default)).DocumentExtractionInstructions=="","an administrator can explicitly clear extraction instructions");
 Check(other.DriverCostPerHour==0,"global rate isolated per company");
 Check(other.Navigation is {Optimization:true,DataEntry:true},"menu preferences isolated per tenant");
 Check(!other.Nova.HasPassword && !other.DispatchTrack.HasApiKey,"second tenant sees no first-tenant connections");
 await using(var db=await factory.OpenConnectionAsync())Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings WHERE tenant_id=@a",new{a})==0,"database row security hides another tenant");
 context.Company=a;
 var batch=await history.Start("C:\\fakepath\\shipping.pdf",default);
 var entry=(await history.List(default)).Single();
 Check(entry.FileName=="shipping.pdf" && entry.Status=="Converting","import history sanitizes filenames and records start");
 await Reject(()=>history.Validate(batch,default),"unfinished conversion cannot receive order credits");
 await history.Converted(batch,2,default);
 await history.Validate(batch,default);
 await history.RecordCreated(batch,"123");await history.RecordCreated(batch,"123");
 entry=(await history.List(default)).Single();
 Check(entry.CreatedCount==1 && entry.ExtractedCount==2 && entry.Status=="Orders created","history counts unique successful orders");
 Check(entry.CreatedPros.SequenceEqual(new[]{"123"}),"history returns distinct created PROs for order links");
 await history.RequireCreatedOrder(batch,"123",default);
 Check(true,"created order link resolves within tenant history");
 try{await history.RequireCreatedOrder(batch,"999",default);throw new Exception("Unrelated order link accepted");}catch(KeyNotFoundException){Check(true,"history link cannot address an unrelated PRO");}
 var failed=await history.Start("pasted-orders.txt",default);await history.Failed(failed);
 Check((await history.List(default)).First().Status=="Failed","failed conversion retained in recent imports");
 context.Company=b;
 Check(!(await history.List(default)).Any(),"history isolated by tenant");
 try{await history.RequireCreatedOrder(batch,"123",default);throw new Exception("Cross-tenant order link accepted");}catch(KeyNotFoundException){Check(true,"order link cannot cross tenant boundaries");}
 await Reject(()=>history.Validate(batch,default),"another tenant cannot attach to import");
 await history.RecordCreated(batch,"999");context.Company=a;
 Check((await history.List(default)).Single(x=>x.Id==batch).CreatedCount==1,"cross-tenant history update blocked");
 await owner.ExecuteAsync("DELETE FROM \"AspNetUserRoles\" WHERE \"TenantId\"=@a AND \"UserId\"=@admin",new{a,admin});
 await using(var db=await factory.OpenConnectionAsync())
 {
  Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings")==0,"ordinary planner cannot read connection settings");
  var menu=JsonSerializer.Deserialize<TenantNavigationSettings>((await db.ExecuteScalarAsync<string>("SELECT public.aurora_tenant_navigation()"))!);
  Check(menu is {Optimization:false,DataEntry:true},"ordinary member can read menu preferences without secrets");
  Check((await history.List(default)).Count()==2,"ordinary member can view company import history");
  Check(await db.ExecuteScalarAsync<decimal>("SELECT public.aurora_driver_cost_per_hour()")==35.25m,"ordinary planner can use only the global labor rate");
 }
 context.User=Guid.NewGuid();
 Check(!(await history.List(default)).Any(),"nonmember cannot read import history");
 await Reject(()=>history.Validate(batch,default),"another user cannot attach to import");
 await using(var db=await factory.OpenConnectionAsync())
 {
  Check(await db.ExecuteScalarAsync<decimal?>("SELECT public.aurora_driver_cost_per_hour()") is null,"nonmember cannot read driver rate");
  Check(await db.ExecuteScalarAsync<string?>("SELECT public.aurora_tenant_navigation()") is null,"nonmember cannot read menu preferences");
  Check(await db.ExecuteScalarAsync<string?>("SELECT public.aurora_document_extraction_instructions()") is null,"nonmember cannot read extraction instructions");
  Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings")==0,"non-admin cannot read settings");
  Check(await db.ExecuteAsync("UPDATE tenant_settings SET revision=revision+1")==0,"non-admin cannot change settings");
  Check(await db.ExecuteScalarAsync<string?>("SELECT public.aurora_tenant_nova_connection()") is null,"nonmember cannot resolve tenant Nova credentials");
 }
 context.User=admin;
 // Exercise both conversion paths with tenant-scoped PostgreSQL access and an in-memory Hub.
 using var hub=new SampleHub();
 var hubSettings=new ConfigurationManager();
 foreach(var id in new[]{a,b})
 {
  hubSettings[$"DocumentConversion:Tenants:{id:D}:BaseUrl"]="https://hub.example.invalid";
  hubSettings[$"DocumentConversion:Tenants:{id:D}:ServiceCredential"]=id.ToString();
 }
 var options=Options.Create(new FirebirdRoutingOptions());
 var conversion=new OrderDocumentConversion(context,hubSettings,hub,factory,history,new FirebirdRoutingSource(factory,context,options),options);
 using var sampleStream=new MemoryStream("sample order"u8.ToArray());
 var sample=new FormFile(sampleStream,0,sampleStream.Length,"file","sample.txt");
 const string unsaved="Use the reference in the second column.";
 var preview=await conversion.Convert(sample,unsaved,default);
 Check(hub.Prompt.Contains(globalRules),"sample preview includes the shared global instructions");
 var blockedPreview=await conversion.Convert(sample,unsaved,default,"Unauthorized global draft");
 Check(blockedPreview is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult,"ordinary member cannot override global instructions in previews");
 var samples=(preview as IValueHttpResult)?.Value as DocumentExtractionSample[];
 Check(samples is [{Pro:"42"}] && samples[0].Fields.GetProperty("TagNumber").GetString()=="MCO42" && hub.Prompt.EndsWith(unsaved) && !hub.Prompt.Contains(companyInstructions),"preview uses unsaved instructions and returns extracted fields");
 Check((await history.List(default)).Count()==2,"a sample preview leaves import history alone");
 var regular=await conversion.Convert(sample,null,default);
 var drafts=(regular as IValueHttpResult)?.Value as List<OrderDocumentDraft>;
 Check(drafts is [{ImportId:not null}] && hub.Prompt.EndsWith(companyInstructions) && !hub.Prompt.Contains(unsaved),"ordinary member conversion uses the saved instructions after a preview");
 Check(hub.Prompt.Contains("Return ONLY a JSON array") && hub.Credential==a.ToString(),"shared response contract and authenticated tenant credential reach the Hub");
 context.Company=b;
 await conversion.Convert(sample,null,default);
 Check(hub.Prompt.Contains("Return ONLY a JSON array") && !hub.Prompt.Contains(companyInstructions) && hub.Credential==b.ToString(),"a cleared tenant uses the shared prompt and its own Hub credential");
 Check(hub.Prompt.Contains(globalRules),"global rules apply even when a tenant clears company instructions");
 context.Company=a;
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
 DELETE FROM order_import_history WHERE tenant_id=@id;
 DELETE FROM aurora_sync_connection WHERE tenant_id=@id;
 DELETE FROM tenant_settings WHERE tenant_id=@id;
 DELETE FROM "AspNetUserRoles" WHERE "TenantId"=@id;
 DELETE FROM user_tenant WHERE tenant_id=@id;
 DELETE FROM tenant_product WHERE tenant_id=@id;
 DELETE FROM tenant WHERE id=@id;
 """,new{id});
}
Console.WriteLine($"{count} tenant settings checks passed; fixtures removed.");
sealed class SampleHub : HttpMessageHandler, IHttpClientFactory
{
 public string Prompt {get;private set;}="";
 public string? Credential {get;private set;}
 public HttpClient CreateClient(string name)=>new(this,disposeHandler:false);
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
 {
  var multipart=(MultipartFormDataContent)request.Content!;
  Prompt=await multipart.Single(p=>p.Headers.ContentDisposition?.Name?.Trim('"')=="prompt").ReadAsStringAsync(ct);
  Credential=request.Headers.Authorization?.Parameter;
  return new(System.Net.HttpStatusCode.OK){Content=new StringContent("""[{"TagNumber":"MCO42","Order":{"Customer":"Receiver"}}]""")};
 }
}

sealed class Context(Guid company,Guid user):ITenantContext
{
 public Guid Company=company,User=user;
 public Guid? TenantIdOrNull=>Company;
 public Guid? UserIdOrNull=>User;
}
