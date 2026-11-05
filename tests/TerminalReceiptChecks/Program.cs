using Aurora.Integrations.Nova;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

// A new disposable embedded Firebird database; no tenant data or credentials.
var client=Environment.GetEnvironmentVariable("FIREBIRD_EMBEDDED_CLIENT") ?? @"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll";
if(!File.Exists(client))throw new Exception("Set FIREBIRD_EMBEDDED_CLIENT to the installed embedded Firebird client.");
var file=Path.Combine(Path.GetTempPath(),"aurora-terminal-check-"+Guid.NewGuid().ToString("N")+".fdb");
var cs=new FbConnectionStringBuilder{Database=file,ServerType=FbServerType.Embedded,ClientLibrary=client,UserID="SYSDBA",Charset="UTF8",Pooling=false}.ConnectionString;
FbConnection.CreateDatabase(cs);
var checks=0;
try
{
    await using var db=new FbConnection(cs);await db.OpenAsync();
    foreach(var ddl in new[]{
        "CREATE TABLE CT_STAT(CTS_ID BIGINT PRIMARY KEY,CTS_CODE VARCHAR(12))",
        "CREATE TABLE LOCMAST(LM_ID VARCHAR(12) PRIMARY KEY)",
        "CREATE TABLE OPS_HEADER(PB_ID BIGINT PRIMARY KEY,PB_TYPE VARCHAR(1),PB_LOCATION VARCHAR(12),PB_STATUS BIGINT,PB_DT_CANC TIMESTAMP,PB_STATUS_LOC_TYPE VARCHAR(1),PB_STATUS_LOCATION VARCHAR(12))",
        "CREATE TABLE OPS_STOP_REC(OPS_STOP_ID BIGINT PRIMARY KEY,OPS_ORDER_ID BIGINT,OPS_TYPE VARCHAR(1),OPS_STATUS BIGINT,OPS_LOC_TYPE VARCHAR(1),OPS_LOCATION VARCHAR(12),OPS_MANIFEST_NUMBER BIGINT,OPS_DELIVERED_TIME TIMESTAMP,OPS_TO_AGENT_TIME TIMESTAMP,OPS_PICKED_UP VARCHAR(1))",
        "CREATE TABLE OPS_MANIFEST_STOPS(OSM_STOP_ID BIGINT,OSM_PKUP_DELV_ID BIGINT)"}) await db.ExecuteAsync(ddl);
    foreach(var pair in new[]{(1,"READY"),(2,"DOCK"),(3,"ENR"),(4,"CPLT"),(5,"ARI"),(6,"PEND")})
        await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(@id,@code)",new{id=pair.Item1,code=pair.Item2});
    await db.ExecuteAsync("INSERT INTO LOCMAST VALUES('A')");await db.ExecuteAsync("INSERT INTO LOCMAST VALUES('B')");
    await using var tx=await db.BeginTransactionAsync();
    async Task Exec(string sql)=>_ = await db.ExecuteAsync(sql,transaction:tx);
    async Task Check(bool expected,string terminal,string name)
    {
        var rows=(await db.QueryAsync<string>($"SELECT {NativeRoutingSql.Terminal()} FROM OPS_HEADER h JOIN OPS_STOP_REC s ON s.OPS_ORDER_ID=h.PB_ID WHERE {NativeRoutingSql.Ready()}",new{readyStatuses=new[]{1},pickupStatuses=new[]{6}},tx)).ToArray();
        if((rows.Length==1)!=expected || expected&&rows[0]!=terminal)throw new Exception(name+": "+string.Join(',',rows));
        Console.WriteLine("PASS "+name);checks++;
    }
    await Exec("INSERT INTO OPS_HEADER VALUES(10,'O','A',1,NULL,NULL,NULL)");
    await Exec("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_TYPE) VALUES(20,10,'D')");
    await Check(true,"A","Legacy unlocated ready freight retains original terminal");
    await Exec("UPDATE OPS_STOP_REC SET OPS_STATUS=3,OPS_LOC_TYPE='L',OPS_LOCATION='A' WHERE OPS_STOP_ID=20");
    await Check(false,"","In-transit stop overrides stale ready header");
    await Exec("UPDATE OPS_STOP_REC SET OPS_STATUS=5,OPS_LOCATION='B' WHERE OPS_STOP_ID=20");
    await Check(false,"","Arrival alone does not release freight");
    await Exec("INSERT INTO OPS_HEADER VALUES(30,'M','A',4,NULL,'L','B')");
    await Exec("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID) VALUES(40,30)");
    await Exec("INSERT INTO OPS_MANIFEST_STOPS VALUES(40,20)");
    await Exec("UPDATE OPS_STOP_REC SET OPS_STATUS=2,OPS_LOCATION='B',OPS_MANIFEST_NUMBER=30 WHERE OPS_STOP_ID=20");
    await Check(true,"B","Dock receipt at B is ready despite origin A and completed manifest history");
    async Task SaveCheck(bool expected,string terminal,string name)
    {
        if(await FirebirdRoutingSource.RoutingStopAvailable(db,tx,10,20,"D",terminal,[1],[],default)!=expected)throw new Exception(name);
        Console.WriteLine("PASS "+name);checks++;
    }
    await SaveCheck(true,"B","Production export validation accepts received freight at B");
    await SaveCheck(false,"A","Production export validation rejects old-terminal draft");
    if(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS",transaction:tx)!=1)throw new Exception("History changed during validation");
    checks++;Console.WriteLine("PASS Export validation preserves native manifest history");
    await Exec("INSERT INTO OPS_HEADER VALUES(31,'M','B',3,NULL,'L','B')");
    await Exec("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID) VALUES(41,31)");
    await Exec("INSERT INTO OPS_MANIFEST_STOPS VALUES(41,20)");
    await Check(false,"","New active manifest blocks even alongside completed history");
    await SaveCheck(false,"B","Production export rejects reassignment after planning");
    await Exec("DELETE FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=41");
    await Exec("UPDATE OPS_HEADER SET PB_STATUS=3 WHERE PB_ID=30");
    await Check(false,"","Active direct and bridge assignments block receipt");
    await SaveCheck(false,"B","Production export rejects receipt if manifest is reopened");
    await Exec("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=NULL WHERE OPS_STOP_ID=20");
    await Check(false,"","Active bridge alone still blocks");
    await Exec("UPDATE OPS_HEADER SET PB_STATUS=4 WHERE PB_ID=30");
    await Check(true,"B","Completed bridge alone is retained history");
    await Exec("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=999 WHERE OPS_STOP_ID=20");
    await Check(false,"","Unknown direct manifest fails closed");
    await Exec("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=30,OPS_LOCATION='UNKNOWN' WHERE OPS_STOP_ID=20");
    await Check(false,"","Unknown receiving terminal cannot release historical assignment");
    await Exec("UPDATE OPS_STOP_REC SET OPS_LOCATION='B',OPS_LOC_TYPE='A' WHERE OPS_STOP_ID=20");
    await Check(false,"","Account location is not a terminal even if codes match");
    await Exec("UPDATE OPS_STOP_REC SET OPS_LOC_TYPE='L',OPS_DELIVERED_TIME=CURRENT_TIMESTAMP WHERE OPS_STOP_ID=20");
    await Check(false,"","Final delivered freight stays unavailable");
    await SaveCheck(false,"B","Production export rejects freight delivered after planning");
    await Exec("UPDATE OPS_STOP_REC SET OPS_DELIVERED_TIME=NULL,OPS_TO_AGENT_TIME=CURRENT_TIMESTAMP WHERE OPS_STOP_ID=20");
    await Check(false,"","Freight handed to an agent stays unavailable");
    await Exec("UPDATE OPS_STOP_REC SET OPS_TO_AGENT_TIME=NULL WHERE OPS_STOP_ID=20");
    await Exec("UPDATE OPS_HEADER SET PB_DT_CANC=CURRENT_TIMESTAMP WHERE PB_ID=10");
    await Check(false,"","Canceled order stays unavailable");
    await Exec("UPDATE OPS_HEADER SET PB_DT_CANC=NULL,PB_STATUS=2,PB_STATUS_LOC_TYPE='L',PB_STATUS_LOCATION='B' WHERE PB_ID=10");
    await Exec("UPDATE OPS_STOP_REC SET OPS_STATUS=NULL,OPS_LOC_TYPE=NULL,OPS_LOCATION=NULL WHERE OPS_STOP_ID=20");
    await Check(true,"B","Header custody supports native records without stop custody");
    await Exec("UPDATE OPS_HEADER SET PB_STATUS_LOC_TYPE=NULL,PB_STATUS_LOCATION=NULL WHERE PB_ID=10");
    await Check(false,"","Dock without explicit terminal custody does not release a prior manifest");
    await Exec("DELETE FROM OPS_MANIFEST_STOPS");
    await Exec("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=NULL,OPS_TYPE='P',OPS_STATUS=6 WHERE OPS_STOP_ID=20");
    await Check(true,"A","Pickup routing retains existing original-terminal behavior");
    await Exec("UPDATE OPS_STOP_REC SET OPS_PICKED_UP='Y' WHERE OPS_STOP_ID=20");
    await Check(false,"","Completed pickup stays unavailable");
    await tx.RollbackAsync();
}
finally { FbConnection.DropDatabase(cs); }
if(Environment.GetEnvironmentVariable("TERMINAL_RECEIPT_PG") is {} pgConnection)
    checks+=await SnapshotChecks.Run(pgConnection);
Console.WriteLine($"{checks} terminal receipt checks passed.");
