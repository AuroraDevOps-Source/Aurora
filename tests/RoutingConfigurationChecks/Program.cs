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
async Task Reject(Func<Task> action,string name){try{await action();}catch(FormatException){Check(true,name);return;}throw new Exception(name);}
var initial=await source.RoutingConfiguration(default);
Check(initial.ReadyStatuses.SequenceEqual(new[]{110}),"default is Nova Ready To Route 110");
var selected=initial.AvailableStatuses.Select(s=>s.Id).Take(2).ToArray();
Check(selected.Length==2,"multiple Nova order statuses available");
var saved=await source.SaveRoutingConfiguration(new(selected.Concat(selected).ToArray()),default,true);
Check(saved.SequenceEqual(selected.Distinct().Order()),"multiple statuses saved and duplicates removed (rolled back)");
Check((await source.RoutingConfiguration(default)).ReadyStatuses.SequenceEqual(initial.ReadyStatuses),"rollback preserves configuration");
await Reject(()=>source.SaveRoutingConfiguration(new([]),default,true),"empty selection rejected");
await Reject(()=>source.SaveRoutingConfiguration(new([int.MaxValue]),default,true),"unknown status rejected");
var other=new Tenant(Guid.NewGuid());
var disabled=new FirebirdRoutingSource(new NpgsqlConnectionFactory(ds,other),other,options);
await Reject(()=>disabled.RoutingConfiguration(default),"other tenant cannot read configuration");
await Reject(()=>disabled.SaveRoutingConfiguration(new(selected),default,true),"other tenant cannot save configuration");
await using var firebird=new FbConnection(fb.ConnectionString);
await firebird.OpenAsync();
var count=await Dapper.SqlMapper.ExecuteScalarAsync<int>(firebird,"SELECT COUNT(*) FROM OPS_HEADER WHERE PB_TYPE='O' AND PB_STATUS IN @statuses",new{statuses=selected});
var expected=0;
foreach(var status in selected)expected+=await Dapper.SqlMapper.ExecuteScalarAsync<int>(firebird,"SELECT COUNT(*) FROM OPS_HEADER WHERE PB_TYPE='O' AND PB_STATUS=@status",new{status});
Check(count==expected,"Firebird multi-status filter matches combined individual statuses");
Console.WriteLine("Configuration checks passed; saved settings unchanged.");
sealed class Tenant(Guid id):ITenantContext {public Guid? TenantIdOrNull=>id;public Guid? UserIdOrNull=>null;}
