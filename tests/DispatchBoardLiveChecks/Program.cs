using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aurora.Client;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.Nova;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

// Explicit read-only live check. Credentials remain in the existing container and key ring.
if(args is not ["read-only"])throw new Exception("Explicit read-only mode required");
var identity=JsonDocument.Parse(Console.In.ReadToEnd());
var tenant=new Tenant(Guid.Parse(identity.RootElement.GetProperty("tenant").GetString()!),Guid.Parse(identity.RootElement.GetProperty("user").GetString()!));
var config=new ConfigurationBuilder().AddEnvironmentVariables().Build();
await using var ds=NpgsqlDataSource.Create(config["ConnectionStrings:Aurora"]!);
var factory=new NpgsqlConnectionFactory(ds,tenant);
await using var pg=await factory.OpenConnectionAsync();
var saved=JsonDocument.Parse(await pg.ExecuteScalarAsync<string>("SELECT public.aurora_tenant_nova_connection()")??throw new Exception("No saved Nova connection"));
string Field(string name)=>saved.RootElement.GetProperty(name).GetString()!;
var protector=DataProtectionProvider.Create(new DirectoryInfo(config["Hosting:DataProtectionPath"]!),b=>b.SetApplicationName("Aurora")).CreateProtector("Aurora.NovaUserPassword.v1");
var cs=new FbConnectionStringBuilder{DataSource=Field("Host"),Port=saved.RootElement.GetProperty("Port").GetInt32(),Database=Field("Database"),UserID=Field("UserName"),Password=protector.Unprotect(Field("ProtectedPassword")),Charset="UTF8",ConnectionTimeout=10};
var defaults=config.GetSection("FirebirdRouting").Get<FirebirdRoutingOptions>()??new();
var options=new FirebirdRoutingOptions{Enabled=true,TenantId=tenant.TenantIdOrNull!.Value,ConnectionString=cs.ConnectionString,SourceKey=Field("SourceKey"),TimeZoneId=defaults.TimeZoneId,ReadyStatus=defaults.ReadyStatus,ServiceSeconds=defaults.ServiceSeconds,Lykes=defaults.LykesForSource(tenant.TenantIdOrNull.Value,Field("SourceKey"))};
Console.WriteLine($"Source: {cs.DataSource} / {cs.Database}; zone {options.TimeZoneId}");
var source=new FirebirdRoutingSource(factory,tenant,Options.Create(options));
using var cts=new CancellationTokenSource(TimeSpan.FromMinutes(3));
await using(var fb=new FbConnection(cs.ConnectionString)){
 await fb.OpenAsync(cts.Token);
 Console.WriteLine("Native order count: "+await fb.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_TYPE='O' AND PB_DT_CANC IS NULL"));
}
await using(var fb=new FbConnection(cs.ConnectionString)){
 await fb.OpenAsync(cts.Token);
 var statuses=await fb.QueryAsync("SELECT TRIM(t.CTS_CODE) AS Code,TRIM(t.CTS_DESC) AS Label,COUNT(*) AS Total FROM OPS_HEADER h LEFT JOIN CT_STAT t ON t.CTS_ID=h.PB_STATUS WHERE h.PB_TYPE='O' AND h.PB_DT_CANC IS NULL GROUP BY t.CTS_CODE,t.CTS_DESC");
 Console.WriteLine("Native status counts: "+JsonSerializer.Serialize(statuses));
}
var clock=Stopwatch.StartNew();
using var handler=new Adapter(source,new TmsStore(factory,tenant,source));
using var http=new HttpClient(handler){BaseAddress=new Uri("http://live-read-only.invalid/"),Timeout=TimeSpan.FromMinutes(3)};
var today=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId)).Date;
var result=await new DispatchBoardLoader(http).Load(false,today,true,cts.Token);
foreach(var g in result.Items.Where(i=>i.Lane=="Unassigned").GroupBy(i=>i.Status))Console.WriteLine("Unassigned status: "+g.Key+" count="+g.Count()+" undated="+g.Count(i=>i.Scheduled is null));
Console.WriteLine("Loader seconds: "+clock.Elapsed.TotalSeconds.ToString("F2"));
Console.WriteLine("Requests: "+handler.Requests);
foreach(var group in result.Items.Where(i=>DispatchBoardRules.InScope(i,today,true)).GroupBy(i=>i.Lane))Console.WriteLine(group.Key+": "+group.Count());
Console.WriteLine("Resources: "+result.Resources.Count+"; warnings: "+result.Warnings.Count);
foreach(var warning in result.Warnings)Console.WriteLine("WARNING "+warning);
if(result.Warnings.Count>0)throw new Exception("Live board returned warnings");
if(result.Items.Any(i=>i.Status.Equals("Void",StringComparison.OrdinalIgnoreCase)))throw new Exception("Void order leaked into board");
if(result.Items.Where(i=>i.Status=="Unknown").Any(i=>!i.Issues.Any(x=>x.Contains("Unknown Nova status"))))throw new Exception("Unknown status lacks blocker");
Console.WriteLine("PASS native VOID exclusion and unknown-status blockers");
if(result.Items.Select(i=>i.Key).Distinct().Count()!=result.Items.Count)throw new Exception("Duplicate cards");
if(result.Items.Any(i=>i.Lane=="Complete"&&i.Late(DateTimeOffset.UtcNow)))throw new Exception("Completed work marked late");
Console.WriteLine("PASS live board reads, source-zone evaluation, unique cards, completed-work exclusion");

sealed record Tenant(Guid? TenantIdOrNull,Guid? UserIdOrNull):ITenantContext;
sealed class Adapter(FirebirdRoutingSource source,TmsStore tms):HttpMessageHandler
{
 public int Requests;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){
  Interlocked.Increment(ref Requests);
  var uri=request.RequestUri!;var path=uri.AbsolutePath;var q=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);var sw=Stopwatch.StartNew();
  object value;
  if(path=="/api/v1/tms/orders")value=await tms.Orders(null,null,"ReadyToRoute",null,null,ct);
  else if(path.EndsWith("/records/orders"))value=await source.RecordList("orders",null,int.Parse(q["page"]!),ct);
  else if(path.EndsWith("/records/fleet"))value=await source.RecordList("fleet",null,int.Parse(q["page"]!),ct);
  else if(path.EndsWith("/manifests"))value=await source.ReadManifests(null,null,null,true,ct,true);
  else if(path.EndsWith("/drivers"))value=await source.ReadDrivers(ct);
  else if(path.EndsWith("/dispatch"))value=await source.ReadDispatch(long.Parse(path.Split('/')[^2]),ct);
  else if(path.EndsWith("/orders"))value=await source.ReadManifestOrders(long.Parse(path.Split('/')[^2]),ct);
  else throw new Exception("Unexpected live request");
  if(sw.Elapsed.TotalSeconds>2)Console.WriteLine("Slow read "+path+" "+sw.Elapsed.TotalSeconds.ToString("F2")+"s");
  return new(HttpStatusCode.OK){Content=JsonContent.Create(value,value.GetType())};
 }
}



