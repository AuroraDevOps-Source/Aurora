using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Text.Json.Nodes;

static class NovaOrderBridge
{
    public static async Task Run(FbConnection db,string cs,LykesNovaOptions options,JsonObject newOrder,
        Func<JsonObject,Task<JsonObject>> cancelAtFreightOps,Action<bool,string> check)
    {
        async Task Columns(string table,string[] definitions)
        {
            var existing=(await db.QueryAsync<string>("SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME=@table",new{table})).ToHashSet();
            foreach(var definition in definitions)if(!existing.Contains(definition.Split(' ')[0]))await db.ExecuteAsync($"ALTER TABLE {table} ADD {definition}");
        }
        await Columns("OPS_HEADER",["PB_LOAD_TYPE CHAR(1)","PBNUM NUMERIC(9,0)","PBLOCAT VARCHAR(12)","PBSUFFIX VARCHAR(2)","PB_DT_ENT TIMESTAMP","PBINIT VARCHAR(8)","PB_STATUS_TIME TIMESTAMP","PB_EXTERNAL_REF VARCHAR(100)","PBBOL VARCHAR(200)","PBBNAME VARCHAR(200)","PBBADDR1 VARCHAR(200)","PBBADDR2 VARCHAR(200)","PBBCITY VARCHAR(200)","PBBSTATE VARCHAR(200)","PBBZIP VARCHAR(200)","PBBCOUNTRY VARCHAR(200)","PB_NOTES VARCHAR(4000)","PB_TTL_PALLETS INTEGER","PB_TTL_PALLET_POSN INTEGER","PB_DT_RATE TIMESTAMP","PB_DT_AUD TIMESTAMP","PB_DT_BILL DATE","PB_INV_DATE DATE","PBTOTPD DECIMAL(14,4)","PBILPAY DECIMAL(14,4)","PBILREC DECIMAL(14,4)","PB_DISCOUNT_AMT DECIMAL(14,4)","PB_DISCOUNT_PERC DECIMAL(14,4)","PB_COD_AMT DECIMAL(14,4)","PB_MASTER_ID BIGINT","PBCINIT VARCHAR(8)"]);
        await Columns("OPS_STOP_REC",["OPS_STOP_NUMBER INTEGER","OPS_NAME VARCHAR(200)","OPS_ADDR1 VARCHAR(200)","OPS_ADDR2 VARCHAR(200)","OPS_CITY VARCHAR(200)","OPS_STATE VARCHAR(200)","OPS_ZIP VARCHAR(200)","OPS_COUNTRY VARCHAR(200)","OPS_ACCTID VARCHAR(12)","OPS_LATITUDE DOUBLE PRECISION","OPS_LONGITUDE DOUBLE PRECISION","OPS_APPT_LOW TIMESTAMP","OPS_APPT_HIGH TIMESTAMP","OPS_TTL_PALLETS INTEGER","OPS_TTL_PALLET_POSN INTEGER"]);
        foreach(var sql in new[]{"CREATE GENERATOR ORDER_GEN","CREATE GENERATOR OPS_STOP_GEN","CREATE TABLE LOCMAST(LM_ID VARCHAR(12) PRIMARY KEY)","CREATE TABLE CT_STAT(CTS_ID INTEGER PRIMARY KEY,CTS_CODE VARCHAR(12))","INSERT INTO LOCMAST VALUES('N-AUS')","INSERT INTO CT_STAT VALUES(110,'READY')","INSERT INTO CT_STAT VALUES(999,'VOID')"})await db.ExecuteAsync(sql);
        await Columns("OPS_HEADER",["PB_NO_CHRG CHAR(1)"]);
        foreach(var sql in string.Join('\n',File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"OrderKeys.sql")).Where(x=>!x.TrimStart().StartsWith("--"))).Split(';',StringSplitOptions.RemoveEmptyEntries))
            if(!string.IsNullOrWhiteSpace(sql))await db.ExecuteAsync(sql);
        options.OrderLifecycleEnabled=true;options.OperatorInitials="AURTEST";
        var consumer=Guid.NewGuid();await db.ExecuteAsync("INSERT INTO AURORA_SYNC_CONSUMER VALUES(@consumer,'Y')",new{consumer=consumer.ToString()});
        var mapper=new Mapper(options);var provider=new NovaSyncProvider(consumer,cs,mapper);
        var create=new SyncChange(Guid.NewGuid(),consumer,"order",newOrder["Id"]!.GetValue<string>(),1,"upsert",newOrder.ToJsonString(),1,Guid.NewGuid());
        var receipt=await provider.Push(create,default);check(receipt==await provider.Push(create,default),"FreightOps-origin order creates once in Nova through receipt retry");
        mapper.Id=long.Parse(receipt.ExternalId);
        JsonObject normalized;
        await using(var tx=db.BeginTransaction())
        {
            var source=(JsonObject)newOrder.DeepClone();source["TerminalCode"]="N-AUS";source["NativeOrderId"]=mapper.Id;source["UseConsigneeAddress"]=true;source["Status"]="ReadyToRoute";
            normalized=LykesNovaMapping.Project(source,await LykesNovaMapping.ReadLines(db,tx,mapper.Id,default),options);
        }
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id",new{id=mapper.Id})==2&&normalized["Lykes"]!["Lines"]![0]!["LineId"]!.ToString()==newOrder["Lykes"]!["Lines"]![0]!["LineId"]!.ToString(),
            "FreightOps-origin creation retains line ID and creates both native stops");
        mapper.Baseline=normalized;
        var cancelled=await cancelAtFreightOps(normalized);
        var cancel=create with{Id=Guid.NewGuid(),Operation="delete",Payload=cancelled.ToJsonString()};
        var cancelledReceipt=await provider.Push(cancel,default);check(cancelledReceipt==await provider.Push(cancel,default),"real FreightOps cancellation is retry-safe in Nova");
        check(await db.ExecuteScalarAsync<int>("SELECT PB_STATUS FROM OPS_HEADER WHERE PB_ID=@id",new{id=mapper.Id})==999&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@id",new{id=mapper.Id})==1,
            "real endpoint cancellation voids Nova and preserves freight history");
    }
    sealed class Mapper(LykesNovaOptions options):INovaSyncMapper
    {
        public long Id;public JsonObject Baseline=new();
        public async Task<ExternalReceipt> Push(SyncChange change,FbConnection db,FbTransaction tx,CancellationToken ct)
        {
            var data=JsonNode.Parse(change.Payload)!.AsObject();var zone=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            if(change.Operation=="delete")await LykesNovaOrderWriter.Cancel(db,tx,Id,data,Baseline,options,[110],change.ConnectionId,change.Id,zone,ct);
            else Id=await LykesNovaOrderWriter.Create(db,tx,data,options,[110],change.ConnectionId,change.Id,zone,ct);
            return new(Id.ToString(),change.Id.ToString());
        }
        public Task<IReadOnlyList<NovaSyncSnapshot>> Read(NovaChangeEvent change,FbConnection db,FbTransaction tx,CancellationToken ct)=>throw new NotSupportedException();
    }
}
