using System.Text.Json;
using Aurora.Contracts;
using Aurora.Modules.Routing;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Microsoft.Extensions.Options;
using Npgsql;
using FirebirdSql.Data.FirebirdClient;

if(Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG") is not {Length:>0} path)throw new Exception("Set FIREBIRD_CHECK_CONFIG to the local test user-secrets file. All writes roll back.");
var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(path))!;
var pg=new NpgsqlConnectionStringBuilder(config["ConnectionStrings:Aurora"].GetString());
var fb=new FbConnectionStringBuilder(config["FirebirdRouting:ConnectionString"].GetString());
if(pg.Host!="127.0.0.1" || pg.Port!=55432 || pg.Database!="aurora_test" || fb.DataSource!="172.23.0.5" || !fb.Database.Equals(@"D:\db\wayl_test\operations.fdb",StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected test databases.");
await using var ds=NpgsqlDataSource.Create(pg.ConnectionString);
var tenantId=Guid.Parse(config["FirebirdRouting:TenantId"].GetString()!);
var tenant=new Tenant(tenantId);
var options=Options.Create(new FirebirdRoutingOptions{Enabled=true,TenantId=tenantId,SourceKey="wayl_test",ConnectionString=fb.ConnectionString});
var source=new FirebirdRoutingSource(new NpgsqlConnectionFactory(ds,tenant),tenant,options);
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
NovaRecordEdit Edit(NovaRecordDocument doc)=>new(doc.Revision,doc.Parts.ToDictionary(p=>p.Key,p=>p.Fields.ToDictionary(f=>f.Key,f=>f.Value)));
async Task Reject(Func<Task> run,string name){try{await run();throw new Exception("Accepted invalid edit: "+name);}catch(FormatException){Console.WriteLine("PASS "+name);}}
var lookup=await source.RecordOptions(default);Check(lookup.Terminals.Contains("MHT") && lookup.Statuses.Any(s=>s.Id==110),"Nova filter options");
var fleet=await source.RecordList("fleet",null,0,default);Check(fleet.Records.Any(r=>r.Number=="PW") && fleet.Records.All(r=>r.Number!="BARRY"),"fleet uses Nova vehicles and excludes drivers");
var truck=await source.ReadRecord("fleet",fleet.Records.First(r=>r.Number=="PW").Id,default);
var update=Edit(truck);update.Values["header"]["EH_MAX_WEIGHT"]="20000";update.Values["header"]["EH_MAX_CUBE"]="1700";update.Values["header"]["EH_DOMICILE"]="MHT";
var saved=await source.SaveRecord("fleet",truck.Id,update,default,rollbackOnly:true);
Check(saved.Parts[0].Fields.Single(f=>f.Key=="EH_MAX_WEIGHT").Value!.StartsWith("20000"),"fleet capacity update reads back within rollback transaction");
Check((await source.ReadRecord("fleet",truck.Id,default)).Revision==truck.Revision,"fleet business values unchanged after rollback");
await Reject(()=>source.SaveRecord("fleet",truck.Id,update with{Revision="stale"},default,true),"stale edit rejected");
update.Values["header"]["EH_MAX_WEIGHT"]="-1";await Reject(()=>source.SaveRecord("fleet",truck.Id,update,default,true),"negative capacity rejected");
var order=await source.ReadRecord("orders",10927,default);
var edits=Edit(order);edits.Values["header"]["PBTWEIGH"]="501";
var stop=order.Parts.First(p=>p.Key.StartsWith("stop-") && p.Title.StartsWith("Consignee"));
edits.Values[stop.Key]["OPS_APPT_LOW"]="2026-10-09T09:00";edits.Values[stop.Key]["OPS_APPT_HIGH"]="2026-10-09T12:00";
saved=await source.SaveRecord("orders",order.Id,edits,default,true);
Check(saved.Parts[0].Fields.Single(f=>f.Key=="PBTWEIGH").Value!.StartsWith("501"),"order weight and appointment edits read back");
Check((await source.ReadRecord("orders",order.Id,default)).Revision==order.Revision,"order and stops unchanged after rollback");
edits.Values[stop.Key]["OPS_APPT_HIGH"]="2026-10-09T08:00";await Reject(()=>source.SaveRecord("orders",order.Id,edits,default,true),"reversed appointment rejected atomically");
edits=Edit(order);edits.Values[stop.Key]["OPS_ADDR1"]="Test address";saved=await source.SaveRecord("orders",order.Id,edits,default,true);
Check(saved.Parts.Single(p=>p.Key==stop.Key).Fields.Where(f=>f.Key is "OPS_LATITUDE" or "OPS_LONGITUDE").All(f=>f.Value is null),"address change clears stale coordinates");
edits=Edit(order);edits.Values["header"]["PB_STATUS"]="999";await Reject(()=>source.SaveRecord("orders",order.Id,edits,default,true),"unknown order status rejected");
var nextStatus=lookup.Statuses.First(s=>s.Id.ToString()!=order.Parts[0].Fields.Single(f=>f.Key=="PB_STATUS").Value);
edits=Edit(order);edits.Values["header"]["PB_STATUS"]=nextStatus.Id.ToString();
saved=await source.SaveRecord("orders",order.Id,edits,default,true);
Check(saved.Parts[0].Fields.Single(f=>f.Key=="PB_STATUS").Value==nextStatus.Id.ToString(),"existing order status can be changed to a valid database status");
Check((await source.ReadRecord("orders",order.Id,default)).Revision==order.Revision,"status change rolled back without changing business data");
edits=Edit(order);edits.Values["header"]["PB_STATUS"]=null;await Reject(()=>source.SaveRecord("orders",order.Id,edits,default,true),"blank order status rejected");
var list=await source.RecordList("orders","10927",0,default);Check(list.Records.Any(o=>o.Id==10927),"order search returns native record");
foreach (var alias in new[] { "FB-10927", "fb10927" })
    Check((await source.RecordList("orders",alias,0,default)).Records.Any(o=>o.Id==10927),"order search resolves " + alias);
var other=new Tenant(Guid.NewGuid());var disabled=new FirebirdRoutingSource(new NpgsqlConnectionFactory(ds,other),other,options);
await Reject(()=>disabled.ReadRecord("fleet",truck.Id,default),"other tenant cannot read records");
await Reject(()=>disabled.SaveRecord("orders",order.Id,Edit(order),default,true),"other tenant cannot write records");
Console.WriteLine("All Nova record checks passed; no business edits committed.");
sealed class Tenant(Guid id):ITenantContext {public Guid? TenantIdOrNull=>id;public Guid? UserIdOrNull=>null;}
