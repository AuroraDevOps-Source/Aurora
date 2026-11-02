using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Text.Json.Nodes;

static class NovaReverseBridge
{
    public static async Task Run(JsonObject baseline,JsonObject freightOpsSnapshot,LykesNovaOptions options,Action<bool,string> check,
        Func<JsonObject,bool,Task<JsonObject>> lifecycle,JsonObject newOrder,Func<JsonObject,Task<JsonObject>> cancelAtFreightOps,
        Func<FbConnection,string,Task> productionChecks)
    {
        var path=Path.Combine(Path.GetTempPath(),"aurora-reverse-bridge-"+Guid.NewGuid().ToString("N")+".fdb");
        var cs=new FbConnectionStringBuilder{Database=path,UserID="SYSDBA",Password="test",ServerType=FbServerType.Embedded,
            ClientLibrary=@"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll",Pooling=false}.ConnectionString;
        FbConnection.CreateDatabase(cs);
        try
        {
            await using var db=new FbConnection(cs);await db.OpenAsync();
            foreach(var sql in new[]{
                "CREATE TABLE AURORA_SYNC_CONSUMER(CONSUMER_ID VARCHAR(36) PRIMARY KEY,ENABLED CHAR(1))",
                "CREATE TABLE AURORA_SYNC_RECEIPT(CONSUMER_ID VARCHAR(36),CHANGE_ID VARCHAR(36),ENTITY_TYPE VARCHAR(30),ENTITY_ID VARCHAR(80),EXTERNAL_REVISION VARCHAR(128),PAYLOAD_HASH VARCHAR(64),PRIMARY KEY(CONSUMER_ID,CHANGE_ID))",
                "CREATE GENERATOR AURORA_SYNC_EVENT_GEN",
                "CREATE TABLE AURORA_SYNC_EVENT(EVENT_ID BIGINT PRIMARY KEY,CONSUMER_ID VARCHAR(36),ENTITY_TYPE VARCHAR(30),ENTITY_ID VARCHAR(80),CHANGE_KIND CHAR(1))",
                "CREATE TABLE OPS_HEADER(PB_ID BIGINT PRIMARY KEY,PB_TYPE CHAR(1),PB_STATUS INTEGER,PB_DT_CANC TIMESTAMP,PBTOTBIL DECIMAL(14,4),PBCREV DECIMAL(14,4),PB_LOCATION VARCHAR(20),PBTPIECE DECIMAL(14,4),PBTWEIGH DECIMAL(14,4),PB_TTL_CUBE DECIMAL(14,4))",
                "CREATE TABLE OPS_STOP_REC(OPS_STOP_ID BIGINT PRIMARY KEY,OPS_ORDER_ID BIGINT,OPS_TYPE CHAR(1),OPS_RATE SMALLINT,OPS_MANIFEST_NUMBER BIGINT,OPS_DELIVERED_TIME TIMESTAMP,OPS_TTL_PIECES DECIMAL(14,4),OPS_TTL_WEIGHT DECIMAL(14,4),OPS_TTL_CUBE DECIMAL(14,4))",
                "CREATE TABLE KEYWORDB(KW_ID VARCHAR(20) PRIMARY KEY,KW_TOT_PIECES CHAR(1),KW_TOT_WEIGHT CHAR(1))",
                "CREATE TABLE OPS_LINE_ITEMS(OPL_ORDER_ID BIGINT,OPL_SEQUENCE INTEGER,OPL_STOP_ID BIGINT,OPL_SYNC_ID VARCHAR(36) UNIQUE,OPL_KEYWORD VARCHAR(20),OPL_SKU VARCHAR(30),OPL_UOM VARCHAR(20),OPL_CUBE VARCHAR(40),OPL_PIECES DECIMAL(14,4),OPL_WEIGHT DECIMAL(14,4),OPL_KILOS DECIMAL(14,4),OPL_PALLETS INTEGER,OPL_DESC VARCHAR(80),OPL_AMOUNT DECIMAL(14,4),OPL_RATE DECIMAL(14,4),OPL_DIM_WGT DECIMAL(14,4),OPL_TONS DECIMAL(14,4),OPL_MET_TONS DECIMAL(14,4),PRIMARY KEY(OPL_ORDER_ID,OPL_SEQUENCE))"
            })await db.ExecuteAsync(sql);
            foreach(var column in new[]{"PB_DT_RATE TIMESTAMP","PB_DT_AUD TIMESTAMP","PB_DT_BILL DATE","PB_INV_DATE DATE","PBTOTPD DECIMAL(14,4)","PBILPAY DECIMAL(14,4)","PBILREC DECIMAL(14,4)","PB_DISCOUNT_AMT DECIMAL(14,4)","PB_DISCOUNT_PERC DECIMAL(14,4)","PB_COD_AMT DECIMAL(14,4)","PB_MASTER_ID BIGINT","PB_NO_CHRG CHAR(1)"})await db.ExecuteAsync("ALTER TABLE OPS_HEADER ADD "+column);
            await db.ExecuteAsync("CREATE TABLE OPS_MANIFEST_STOPS(OSM_STOP_ID BIGINT,OSM_PKUP_DELV_ID BIGINT)");
            await db.ExecuteAsync("ALTER TABLE OPS_HEADER ADD PB_RATE_NOTES VARCHAR(100)");
            await db.ExecuteAsync("ALTER TABLE OPS_LINE_ITEMS ADD OPL_RATE_TYPE VARCHAR(10)");
            await db.ExecuteAsync("ALTER TABLE OPS_LINE_ITEMS ADD OPL_PALLET_POSITIONS INTEGER");
            foreach(var sql in File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"LineTombstones.sql")).Split(';',StringSplitOptions.RemoveEmptyEntries))
                if(sql.Split('\n').Any(x=>!string.IsNullOrWhiteSpace(x)&&!x.TrimStart().StartsWith("--")))await db.ExecuteAsync(sql);
            var consumer=Guid.NewGuid();
            await db.ExecuteAsync("INSERT INTO AURORA_SYNC_CONSUMER VALUES(@id,'Y')",new{id=consumer.ToString()});
            await db.ExecuteAsync("INSERT INTO KEYWORDB VALUES('FRT','Y','Y')");
            await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_STATUS,PB_LOCATION,PBTPIECE,PBTWEIGH,PB_TTL_CUBE) VALUES(1,'O',110,'N-AUS',3,120,2.5)");
            await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_TYPE,OPS_RATE,OPS_TTL_PIECES,OPS_TTL_WEIGHT,OPS_TTL_CUBE) VALUES(1,1,'P',1,3,120,2.5)");
            await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_TYPE,OPS_RATE) VALUES(2,1,'D',0)");
            var lineId=baseline["Lykes"]!["Lines"]![0]!["LineId"]!.GetValue<string>();
            await db.ExecuteAsync("""
                INSERT INTO OPS_LINE_ITEMS(OPL_ORDER_ID,OPL_SEQUENCE,OPL_STOP_ID,OPL_SYNC_ID,OPL_KEYWORD,OPL_SKU,OPL_UOM,
                  OPL_CUBE,OPL_PIECES,OPL_WEIGHT,OPL_PALLETS)
                VALUES(1,1,1,@lineId,'FRT','NOVA-SKU','CTN','2.5',3,120,1)
                """,new{lineId});
            options.WritesEnabled=true;
            var provider=new NovaSyncProvider(consumer,cs,new Mapper(baseline,options));
            var change=new SyncChange(Guid.NewGuid(),consumer,"order","NOVA-BRIDGE",1,"upsert",freightOpsSnapshot.ToJsonString(),1,Guid.NewGuid());
            var receipt=await provider.Push(change,default);
            check(receipt==await provider.Push(change,default),"real FreightOps reverse payload has retry-safe Nova receipt");
            await using var tx=db.BeginTransaction();
            var rows=await LykesNovaMapping.ReadLines(db,tx,1,default);
            check(rows.Single().LineId==lineId&&rows[0].Pieces==4&&rows[0].Weight==160&&rows[0].Cube=="3.5"&&rows[0].Pallets==1,
                "real FreightOps edit updates the same Nova line and preserves its pallet metadata");
            var source=(JsonObject)freightOpsSnapshot.DeepClone();source["TerminalCode"]="N-AUS";
            var roundTrip=LykesNovaMapping.Project(source,rows,options);
            check(roundTrip["Pieces"]!.GetValue<int>()==4&&roundTrip["Cube"]!.GetValue<decimal>()==14,
                "FreightOps-to-Nova-to-Aurora round trip preserves pieces and physical cube");
            await tx.CommitAsync();
            options.NewFreightKeyword="FRT";
            foreach(var adding in new[]{true,false})
            {
                var desired=await lifecycle(roundTrip,adding);
                var lifecycleProvider=new NovaSyncProvider(consumer,cs,new Mapper(roundTrip,options));
                var lifecycleChange=change with{Id=Guid.NewGuid(),Payload=desired.ToJsonString()};
                var lifecycleReceipt=await lifecycleProvider.Push(lifecycleChange,default);
                check(lifecycleReceipt==await lifecycleProvider.Push(lifecycleChange,default),adding?"real endpoint addition retries safely in Nova":"real endpoint deletion retries safely in Nova");
                await using var read=db.BeginTransaction();
                rows=await LykesNovaMapping.ReadLines(db,read,1,default);
                source=(JsonObject)desired.DeepClone();source["TerminalCode"]="N-AUS";
                roundTrip=LykesNovaMapping.Project(source,rows,options);
                check(rows.Length==(adding?2:1)&&roundTrip["Pieces"]!.GetValue<int>()==(adding?6:4)&&roundTrip["Cube"]!.GetValue<decimal>()==(adding?17:14),
                    adding?"FreightOps addition becomes a Nova line with correct totals":"FreightOps removal deletes only its Nova line and restores totals");
            }
            await NovaOrderBridge.Run(db,cs,options,newOrder,cancelAtFreightOps,check);
            await productionChecks(db,cs);
        }
        finally{FbConnection.ClearAllPools();FbConnection.DropDatabase(cs);}
    }
    sealed class Mapper(JsonObject baseline,LykesNovaOptions options):INovaSyncMapper
    {
        public async Task<ExternalReceipt> Push(SyncChange change,FbConnection db,FbTransaction tx,CancellationToken ct)
        {
            await LykesNovaLineWriter.Apply(db,tx,1,JsonNode.Parse(change.Payload)!.AsObject(),baseline,options,[110],change.ConnectionId,ct);
            return new("1",change.Id.ToString());
        }
        public Task<IReadOnlyList<NovaSyncSnapshot>> Read(NovaChangeEvent change,FbConnection db,FbTransaction tx,CancellationToken ct)=>throw new NotSupportedException();
    }
}
