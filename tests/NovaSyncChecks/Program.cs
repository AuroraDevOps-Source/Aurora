using Aurora.Integrations.Nova.Sync;
using FirebirdSql.Data.FirebirdClient;
using Dapper;
int count=0;
void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
var path=Path.Combine(Path.GetTempPath(),"aurora-sync-"+Guid.NewGuid().ToString("N")+".fdb");
var cs=new FbConnectionStringBuilder{Database=path,UserID="SYSDBA",Password="test",ServerType=FbServerType.Embedded,ClientLibrary=@"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll",Pooling=false}.ConnectionString;
FbConnection.CreateDatabase(cs);
try
{
 await using var db=new FbConnection(cs);await db.OpenAsync();
 foreach(var (table,key,type) in new[]{("EQUIP_HEADER","EH_ID","BIGINT"),("DRIVER_HEADER","DH_ID","BIGINT"),("LOCMAST","LM_ID","VARCHAR(12)"),("ACCOUNT","AC_CUST","VARCHAR(12)"),("OPS_HEADER","PB_ID","BIGINT"),("OPS_STOP_REC","OPS_STOP_ID","BIGINT"),("OPS_MANIFEST_STOPS","OSM_STOP_ID","BIGINT")})
  await db.ExecuteAsync($"CREATE TABLE {table}({key} {type} PRIMARY KEY,TEST_VALUE VARCHAR(20))");
 await db.ExecuteAsync("ALTER TABLE OPS_STOP_REC ADD OPS_ORDER_ID BIGINT");
 await db.ExecuteAsync("ALTER TABLE OPS_MANIFEST_STOPS ADD OSM_PKUP_DELV_ID BIGINT");
 var script=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ChangeCapture.sql"));
 var pieces=script.Split("SET TERM ^;");
 foreach(var statement in pieces[0].Split(';',StringSplitOptions.RemoveEmptyEntries).Where(s=>!string.IsNullOrWhiteSpace(s)))await db.ExecuteAsync(statement);
 foreach(var statement in pieces[1].Split("SET TERM ;^")[0].Split('^',StringSplitOptions.RemoveEmptyEntries).Where(s=>!string.IsNullOrWhiteSpace(s)))await db.ExecuteAsync(statement);
 var consumer=Guid.NewGuid();var other=Guid.NewGuid();
 await db.ExecuteAsync("INSERT INTO AURORA_SYNC_CONSUMER(CONSUMER_ID,ENABLED) VALUES(@id,'Y')",new{id=consumer.ToString()});
 await db.ExecuteAsync("INSERT INTO AURORA_SYNC_CONSUMER(CONSUMER_ID,ENABLED) VALUES(@id,'Y')",new{id=other.ToString()});
 await using(var tx=db.BeginTransaction()) {await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID) VALUES(1)",transaction:tx);await tx.RollbackAsync();}
 Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT")==0,"rolled-back Nova write emits no event");
 await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID) VALUES(2)");
 await using(var tx=db.BeginTransaction()){
  var events=await NovaChangeQueue.Read(db,tx,consumer,default);
  Check(events.Length==1&&events[0].EntityId=="2"&&events[0].ChangeKind=="U","native insert captured for consumer");
  await NovaChangeQueue.Acknowledge(db,tx,consumer,events.Select(e=>e.EventId).ToArray(),default);await tx.CommitAsync();
 }
 Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@id",new{id=other.ToString()})==1,"acknowledgement leaves other consumer untouched");
 await using(var tx=db.BeginTransaction()){
  await NovaChangeQueue.SetOrigin(db,tx,consumer,default);
  await db.ExecuteAsync("UPDATE OPS_HEADER SET TEST_VALUE='Aurora' WHERE PB_ID=2",transaction:tx);await tx.CommitAsync();
 }
 Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@id",new{id=consumer.ToString()})==0,"Aurora write does not echo to its own consumer");
 await db.ExecuteAsync("UPDATE OPS_HEADER SET TEST_VALUE='Nova' WHERE PB_ID=2");
 Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@id",new{id=consumer.ToString()})==1,"origin marker does not leak into later transaction");
 await db.ExecuteAsync("DELETE FROM OPS_HEADER WHERE PB_ID=2");
 Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@id AND CHANGE_KIND='D'",new{id=consumer.ToString()})==1,"native deletion captured as tombstone");
 await db.ExecuteAsync("DELETE FROM AURORA_SYNC_EVENT");
 await using var secondDb=new FbConnection(cs);await secondDb.OpenAsync();
 await using var early=db.BeginTransaction();
 await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID) VALUES(10)",transaction:early);
 await secondDb.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID) VALUES(11)");
 await using(var tx=secondDb.BeginTransaction()){
  var events=await NovaChangeQueue.Read(secondDb,tx,consumer,default);
  Check(events.Length==1&&events[0].EntityId=="11","later generator ID can commit first");
  await NovaChangeQueue.Acknowledge(secondDb,tx,consumer,events.Select(e=>e.EventId).ToArray(),default);await tx.CommitAsync();
 }
 await early.CommitAsync();
 await using(var tx=secondDb.BeginTransaction()){
  var events=await NovaChangeQueue.Read(secondDb,tx,consumer,default);
  Check(events.Length==1&&events[0].EntityId=="10","late commit with earlier generator ID is never skipped");
 }
 await ReceiptChecks.Run(cs,consumer,Check);
 await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID) VALUES(200,100)");
 await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID) VALUES(201,11)");
 await db.ExecuteAsync("INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_PKUP_DELV_ID) VALUES(200,201)");
 await db.ExecuteAsync("DELETE FROM AURORA_SYNC_EVENT");
 await db.ExecuteAsync("DELETE FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=200");
 var parents=(await db.QueryAsync<string>("SELECT TRIM(ENTITY_ID) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@consumer",new{consumer=consumer.ToString()})).ToArray();
 Check(parents.Contains("100")&&parents.Contains("11"),"membership deletion captures both parent identities before relationship disappears");
 await db.ExecuteAsync("DELETE FROM AURORA_SYNC_EVENT");
 await db.ExecuteAsync("DELETE FROM OPS_STOP_REC WHERE OPS_STOP_ID=201");
 Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@consumer AND ENTITY_ID='11'",new{consumer=consumer.ToString()})==1,"stop deletion captures its parent for later aggregate import");
 Console.WriteLine($"{count} Nova change capture checks passed.");
}
finally{FbConnection.ClearAllPools();FbConnection.DropDatabase(cs);}
