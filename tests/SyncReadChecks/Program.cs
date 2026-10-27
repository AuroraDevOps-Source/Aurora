using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.Nova;
using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using Npgsql;

var pgConnection=Environment.GetEnvironmentVariable("SYNC_TEST_CONNECTION")??throw new Exception("Disposable PostgreSQL connection required.");
if(!new NpgsqlConnectionStringBuilder(pgConnection).Database!.StartsWith("aurora_sync_check_",StringComparison.Ordinal))throw new Exception("Use a disposable sync database.");
var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Environment.GetEnvironmentVariable("SYNC_READ_CONFIG")!))!.ToDictionary(p=>p.Key,p=>p.Value.ToString());
var options=new FirebirdRoutingOptions{Enabled=true,TenantId=Guid.Parse(config["FirebirdRouting:TenantId"]),SourceKey=config["FirebirdRouting:SourceKey"],ConnectionString=config["FirebirdRouting:ConnectionString"],TimeZoneId=config["FirebirdRouting:TimeZoneId"],ReadyStatus=int.Parse(config["FirebirdRouting:ReadyStatus"])};
var native=new FbConnectionStringBuilder(options.ConnectionString);
if(!native.Database.Equals(@"D:\db\wayl_test\operations.fdb",StringComparison.OrdinalIgnoreCase))throw new Exception("Only the authorized WayTest source may be inspected.");
await using var dataSource=NpgsqlDataSource.Create(pgConnection);var tenant=new Context(options.TenantId);var factory=new NpgsqlConnectionFactory(dataSource,tenant);
await using var pg=await factory.OpenConnectionAsync();
var connection=Guid.NewGuid();
connection=await pg.QuerySingleAsync<Guid>("INSERT INTO aurora_sync_connection(tenant_id,id,provider,source_identity,credential_reference,enabled) VALUES(@TenantId,@connection,'nova','nova-719a21c88e9ed0530b79e871aadbc02f','01a08138-7cb9-7a10-8bd8-450a7bfd755e',true) ON CONFLICT(tenant_id) DO UPDATE SET enabled=true,source_identity=excluded.source_identity,credential_reference=excluded.credential_reference RETURNING id",new{tenant.TenantId,connection});
options.SourceKey=await pg.QuerySingleAsync<string>("SELECT source_identity FROM public.aurora_sync_credentials()",new{tenant.TenantId});
await pg.ExecuteAsync("UPDATE aurora_sync_connection SET source_identity=@SourceKey WHERE tenant_id=@TenantId",new{options.SourceKey,tenant.TenantId});
var source=new FirebirdRoutingSource(factory,tenant,Options.Create(options));var mapper=source.ForSynchronization(connection);
if(Environment.GetEnvironmentVariable("SYNC_CHECK_PUSH")=="1")
{
    await PushChecks.Run(pg,tenant.TenantId,connection,options.ConnectionString,mapper);
    return;
}
await using var db=new FbConnection(options.ConnectionString);await db.OpenAsync();
await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait});
int total=0,failures=0;
var only=Environment.GetEnvironmentVariable("SYNC_CHECK_KIND");
var importer=new SyncImporter(factory,tenant,new OperationalSyncEntities(tenant,new DriverStore(factory,tenant)));
var import=Environment.GetEnvironmentVariable("SYNC_CHECK_IMPORT")=="1";
var run=Guid.NewGuid().ToString();
if(import)await NovaLegacyAdoption.Apply(factory,tenant,connection,options.SourceKey,default);
foreach(var query in new[]{("terminal","SELECT TRIM(LM_ID) FROM LOCMAST"),("customer","SELECT TRIM(AC_CUST) FROM ACCOUNT"),("equipment","SELECT CAST(EH_ID AS VARCHAR(80)) FROM EQUIP_HEADER WHERE EH_DELETE_DATE IS NULL"),("order-or-manifest","SELECT CAST(PB_ID AS VARCHAR(80)) FROM OPS_HEADER WHERE PB_TYPE='O' AND PB_DT_CANC IS NULL ORDER BY PB_ID DESC"),("order-or-manifest","SELECT CAST(PB_ID AS VARCHAR(80)) FROM OPS_HEADER WHERE PB_TYPE='M' AND PB_DT_CANC IS NULL ORDER BY PB_ID DESC")} )
{
    if(only=="manifest" ? !query.Item2.Contains("PB_TYPE='M'") : only is not null && only!=query.Item1)continue;
    var ids=(await db.QueryAsync<string>(query.Item2,transaction:tx)).ToArray();
    foreach(var id in ids)
    {
        try
        {
            var snapshots=await mapper.Read(new(1,query.Item1,id,"U"),db,tx,default);
            foreach(var snapshot in snapshots.Where(s=>!s.Deleted))
                switch(snapshot.EntityType)
                {
                    case "terminal":snapshot.Data.Deserialize<TerminalDto>()!.Validate();break;
                    case "customer":snapshot.Data.Deserialize<CustomerDto>()!.Validate();break;
                    case "driver":snapshot.Data.Deserialize<NovaDriver>()!.Validate();break;
                    case "order":snapshot.Data.Deserialize<TmsOrder>()!.Validate();break;
                    case "manifest":snapshot.Data.Deserialize<TmsManifest>()!.Validate();break;
                }
            if(import)
                foreach(var snapshot in snapshots)
                    await importer.Apply(connection,new ExternalBatch([new ExternalChange($"{run}:{query.Item1}:{id}:{snapshot.EntityType}:{snapshot.ExternalId}",snapshot.EntityType,snapshot.ExternalId,snapshot.Revision,snapshot.Data,snapshot.Deleted)],null),default);
            if(import)
            {
                foreach(var snapshot in snapshots)
                    await importer.Apply(connection,new ExternalBatch([new ExternalChange($"{run}:repeat:{query.Item1}:{id}:{snapshot.EntityType}:{snapshot.ExternalId}",snapshot.EntityType,snapshot.ExternalId,snapshot.Revision,snapshot.Data,snapshot.Deleted)],null),default);
            }
            total++;
        }
        catch(Exception ex){failures++;Console.WriteLine($"FAIL {query.Item1} {id}: {ex.GetType().Name}: {ex.Message}");}
    }
}
await tx.RollbackAsync();
if(import)
{
    var echoes=await pg.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM aurora_sync_outbox WHERE tenant_id=@TenantId AND connection_id=@connection AND state IN ('pending','working','conflict')",new{tenant.TenantId,connection});
    if(echoes!=0){failures++;Console.WriteLine($"FAIL repeat imports produced {echoes} outbound changes.");}
    else Console.WriteLine("PASS initial + repeat imports create no outbound echo writes.");
}
Console.WriteLine($"Native read-only projection checks: {total} passed, {failures} failed. No native writes.");
if(failures>0)Environment.ExitCode=1;
sealed record Context(Guid TenantId):ITenantContext{public Guid? TenantIdOrNull=>TenantId;public Guid? UserIdOrNull=>null;}
