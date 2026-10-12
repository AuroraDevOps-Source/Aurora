using Aurora.Contracts;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Aurora.Modules.Routing;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Npgsql;
using FirebirdSql.Data.FirebirdClient;

void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
var catalog=FirebirdRoutingSource.MapFleet([
 new(){NativeId=1,Unit="A",Terminal="MHT",Description="Straight truck",Weight=5000,Cubes=800,StartTime="0730",EndTime="1630"},
 new(){NativeId=2,Unit="B",Terminal="MHT",Description="Straight truck",Weight=2500,Cubes=400},
 new(){NativeId=3,Unit="C",Terminal="PWM",Description="Straight truck",Weight=2000,Cubes=300},
 new(){NativeId=4,Unit="EMPTY",Terminal="MHT",Description="Straight truck"},
 new(){NativeId=5,Unit="TRACTOR",Terminal="MHT",Description="Tractor",Weight=40000,Cubes=1000}
],"-04:00");
Check(FleetPlanning.Eligible(catalog,"MHT").Select(u=>u.Id).SequenceEqual(new[]{"A","B"}),"terminal filtering excludes unconfigured units and power-only tractors");
Check(catalog.Types.Single(t=>t.Code=="NOVA-2").Weight==2500,"per-unit capacities stay distinct");
Check(catalog.Units[0].ShiftStart=="07:30" && catalog.Units[0].ShiftEnd=="16:30","Nova HHmm shifts mapped");
try{FirebirdRoutingSource.MapFleet([new(){NativeId=1,Unit="A"},new(){NativeId=2,Unit="a"}],"-04:00");throw new Exception("Duplicate unit accepted");}catch(FormatException){Console.WriteLine("PASS duplicate unit numbers rejected");}
if(Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG") is not {Length:>0} path){Console.WriteLine("Live checks skipped: no connection configuration.");return;}
var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(path))!;
var tenantId=Guid.Parse(config["FirebirdRouting:TenantId"].GetString()!);
var tenant=new Tenant(tenantId);await using var ds=NpgsqlDataSource.Create(config["ConnectionStrings:Aurora"].GetString()!);
var fb=new FbConnectionStringBuilder(config["FirebirdRouting:ConnectionString"].GetString()){ConnectionTimeout=5};
var sourceKey=config["FirebirdRouting:SourceKey"].GetString()!;
if(Environment.GetEnvironmentVariable("NOVA_USE_SAVED")=="1") {
 await using var pg=await ds.OpenConnectionAsync();
 var userId=await pg.QuerySingleAsync<Guid>("SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"UserName\"='bbatts'");
 await pg.ExecuteAsync("SELECT set_config('app.tenant_id',@tenant,false),set_config('app.user_id',@user,false)",new{tenant=tenantId.ToString(),user=userId.ToString()});
 var json=await pg.QuerySingleOrDefaultAsync<string>("SELECT nova_connection::text FROM user_tenant WHERE tenant_id=@tenantId AND user_id=@userId",new{tenantId,userId});
 if(json is not null){
  using var doc=JsonDocument.Parse(json);var saved=doc.RootElement;
  var protection=DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ASP.NET","DataProtection-Keys")),o=>o.SetApplicationName(Path.GetFullPath("src/Aurora.Api")+Path.DirectorySeparatorChar));
  fb=new(){DataSource=saved.GetProperty("Host").GetString(),Port=saved.GetProperty("Port").GetInt32(),Database=saved.GetProperty("Database").GetString(),UserID=saved.GetProperty("UserName").GetString(),Password=protection.CreateProtector("Aurora.NovaUserPassword.v1").Unprotect(saved.GetProperty("ProtectedPassword").GetString()!),Charset="UTF8",ConnectionTimeout=5};
  sourceKey=saved.GetProperty("SourceKey").GetString()!;
  Console.WriteLine("Testing saved Nova connection: "+fb.DataSource);
 }
}
var source=new FirebirdRoutingSource(new NpgsqlConnectionFactory(ds,tenant),tenant,Options.Create(new FirebirdRoutingOptions{Enabled=true,TenantId=tenantId,SourceKey=sourceKey,ConnectionString=fb.ConnectionString}));
try {
var fleet=await source.ReadFleet(default);var list=await source.RecordList("fleet",null,0,default);
Check(list.Records.All(r=>fleet.Units.Any(u=>u.Id==r.Number)),"planning fleet matches native fleet screen");
var terminals=await source.ReadTerminals(default);Check(terminals.Length>0,"Nova terminals loaded");
Console.WriteLine($"Nova has {fleet.Units.Count} equipment records; {fleet.Units.Count(u=>u.UseForRouting)} have a terminal and both capacities configured.");
if(Environment.GetEnvironmentVariable("NOVA_CHECK_SYNC")=="1"){
 var synced=await source.Synchronize(default);
 var orders=await new TmsStore(new NpgsqlConnectionFactory(ds,tenant),tenant).Orders(null,null,"ReadyToRoute",null,null,default);
 var current=orders.Where(o=>synced.SourceIds.Contains(o.SourceId)).ToArray();
 Check(current.All(o=>terminals.Any(t=>t.Id==o.TerminalId && t.Code==o.TerminalCode)),"planning order snapshots use native terminal identities");
 Console.WriteLine($"Refreshed {synced.Orders} Nova orders.");
}
foreach(var kind in new[]{"terminals","customers"}){
 var rows=await source.ReadMasters(kind,null,0,default);Check(rows.Records.Length>0,kind+" list loaded");
 var original=await source.ReadMaster(kind,rows.Records[0].Id,default);
 var values=original.Fields.ToDictionary(f=>f.Key,f=>f.Value);var name=kind=="terminals"?"LM_NAME":"AC_NAME";values[name]="Rollback check";
 var saved=await source.SaveMaster(kind,original.Id,new(original.Revision,values),default,true);
 Check(saved.Fields.Single(f=>f.Key==name).Value=="Rollback check",kind+" edit reads back within rollback");
 Check((await source.ReadMaster(kind,original.Id,default)).Revision==original.Revision,kind+" rollback preserves original");
 try{await source.SaveMaster(kind,original.Id,new("stale",values),default,true);throw new Exception("Stale revision accepted");}catch(FormatException){Console.WriteLine("PASS "+kind+" stale edit rejected");}
 values=original.Fields.ToDictionary(f=>f.Key,f=>f.Value);values[kind=="terminals"?"LM_LATITUDE":"AC_LATITUDE"]="91";
 try{await source.SaveMaster(kind,original.Id,new(original.Revision,values),default,true);throw new Exception("Invalid coordinate accepted");}catch(FormatException){Console.WriteLine("PASS "+kind+" invalid coordinates rejected");}
}
} catch(Exception ex) when(ex is FbException or TimeoutException){Console.WriteLine("Live Nova connection unavailable; database checks did not complete.");Environment.ExitCode=2;}
sealed class Tenant(Guid id):ITenantContext{public Guid? TenantIdOrNull=>id;public Guid? UserIdOrNull=>null;}

