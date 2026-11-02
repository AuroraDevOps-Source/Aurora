using Aurora.Contracts;
using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Text.Json;
using System.Text.Json.Nodes;

static class OrderLifecycleChecks
{
    public static async Task Run(string cs,FbConnection db,Guid consumer,Action<bool,string> check)
    {
        async Task Columns(string table,string[] definitions)
        {
            var existing=(await db.QueryAsync<string>("SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME=@table",new{table})).ToHashSet();
            foreach(var definition in definitions)if(!existing.Contains(definition.Split(' ')[0]))await db.ExecuteAsync($"ALTER TABLE {table} ADD {definition}");
        }
        await Columns("OPS_HEADER",[
            "PB_LOAD_TYPE CHAR(1)","PBNUM NUMERIC(9,0)","PBLOCAT VARCHAR(12)","PBSUFFIX VARCHAR(2)","PB_DT_ENT TIMESTAMP","PBINIT VARCHAR(8)","PB_STATUS_TIME TIMESTAMP","PB_EXTERNAL_REF VARCHAR(100)",
            "PBBOL VARCHAR(200)","PBBNAME VARCHAR(200)","PBBADDR1 VARCHAR(200)","PBBADDR2 VARCHAR(200)","PBBCITY VARCHAR(200)","PBBSTATE VARCHAR(200)","PBBZIP VARCHAR(200)","PBBCOUNTRY VARCHAR(200)","PB_NOTES VARCHAR(4000)",
            "PB_DT_RATE TIMESTAMP","PB_DT_AUD TIMESTAMP","PB_DT_BILL DATE","PB_INV_DATE DATE","PBTOTPD DECIMAL(14,4)","PBILPAY DECIMAL(14,4)","PBILREC DECIMAL(14,4)","PB_DISCOUNT_AMT DECIMAL(14,4)","PB_DISCOUNT_PERC DECIMAL(14,4)","PB_COD_AMT DECIMAL(14,4)","PB_MASTER_ID BIGINT","PBCINIT VARCHAR(8)"
        ]);
        await Columns("OPS_STOP_REC",["OPS_STOP_NUMBER INTEGER","OPS_NAME VARCHAR(200)","OPS_ADDR1 VARCHAR(200)","OPS_ADDR2 VARCHAR(200)","OPS_CITY VARCHAR(200)","OPS_STATE VARCHAR(200)","OPS_ZIP VARCHAR(200)","OPS_COUNTRY VARCHAR(200)","OPS_ACCTID VARCHAR(12)","OPS_LATITUDE DOUBLE PRECISION","OPS_LONGITUDE DOUBLE PRECISION","OPS_APPT_LOW TIMESTAMP","OPS_APPT_HIGH TIMESTAMP"]);
        await db.ExecuteAsync("CREATE GENERATOR ORDER_GEN");await db.ExecuteAsync("CREATE GENERATOR OPS_STOP_GEN");
        await Columns("OPS_HEADER",["PB_NO_CHRG CHAR(1)"]);
        await db.ExecuteAsync("CREATE TABLE CT_STAT(CTS_ID INTEGER PRIMARY KEY,CTS_CODE VARCHAR(12))");
        await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(110,'READY')");await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(111,'READY2')");await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(999,'VOID')");
        await db.ExecuteAsync("INSERT INTO LOCMAST(LM_ID) VALUES('N-AUS')");
        await db.ExecuteAsync("DELETE FROM AURORA_SYNC_EVENT");
        var options=new LykesNovaOptions{Enabled=true,WritesEnabled=true,OperatorInitials="AURTEST",NewFreightKeyword="FRT",TerminalMappings=new(){{"N-AUS","AUS"}},KeywordKinds=new(){{"FRT","freight"}},PackTypes=new(){{"CTN","C"}},CubeBasis="per-piece",ScalarCubeUnit="ft3",DimensionUnit="in",FreightStopKind="P"};
        var zone=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        JsonObject Data(string pro="900001")
        {
            var data=JsonSerializer.SerializeToNode(new TmsOrder{Id=pro,TerminalCode="AUS",Customer="Receiver",Shipper="Sender",Address="20 Main",OriginAddress="10 Main",
                City="Austin",State="TX",PostalCode="78701",OriginCity="Austin",OriginState="TX",OriginPostalCode="78701",BillTo="Payer",BillToAddress="30 Main",
                Reference="BOL-TEST",Notes="Creation fixture",ScheduledAt=DateTimeOffset.Parse("2026-10-28T10:00:00-04:00"),Pieces=2,Weight=20,Cube=3})!.AsObject();
            data["Lykes"]=new JsonObject{["Version"]=2,["WeightUnit"]="lb",["VolumeUnit"]="ft3",["Lines"]=new JsonArray(new JsonObject{
                ["LineId"]=Guid.NewGuid().ToString(),["Kind"]="freight",["Sku"]="NEW-SKU",["PackType"]="C",["Pieces"]=2,["TotalWeight"]=20,["CubePerPiece"]=1.5m})};
            return data;
        }
        var mapper=new Mapper(options,zone);var provider=new NovaSyncProvider(consumer,cs,mapper);
        SyncChange Change(JsonObject data,string operation="upsert")=>new(Guid.NewGuid(),consumer,"order",data["Id"]!.GetValue<string>(),1,operation,data.ToJsonString(),1,Guid.NewGuid());
        async Task Reject(JsonObject data,string label,string operation="upsert")
        {
            var before=(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER"),await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_STOP_REC"),await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS"),await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_RECEIPT"));
            try{await provider.Push(Change(data,operation),default);throw new Exception("Expected rejection: "+label);}
            catch(Exception e) when(e is FormatException or SyncConflictException){check(true,label);}
            check(before==(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER"),await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_STOP_REC"),await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS"),await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_RECEIPT")),label+" leaves no partial order or receipt");
        }
        await Reject(Data(),"order lifecycle defaults off");options.OrderLifecycleEnabled=true;
        options.OperatorInitials="";await Reject(Data(),"native audit initials required");options.OperatorInitials="AURTEST";
        await Reject(Data(),"new order creation requires manual key migration");
        foreach(var sql in string.Join('\n',File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"OrderKeys.sql")).Where(x=>!x.TrimStart().StartsWith("--"))).Split(';',StringSplitOptions.RemoveEmptyEntries))
            if(!string.IsNullOrWhiteSpace(sql))await db.ExecuteAsync(sql);
        var input=Data();var request=Change(input);var created=await provider.Push(request,default);var id=long.Parse(created.ExternalId);
        var calls=mapper.Calls;check(created==await provider.Push(request,default)&&mapper.Calls==calls,"new order retry returns receipt without creating a second order");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id",new{id})==2&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@id",new{id})==1,
            "one native order, two stops and identified freight commit together");
        check(await db.ExecuteScalarAsync<string>("SELECT PBLOCAT FROM OPS_HEADER WHERE PB_ID=@id",new{id})=="N-AUS"&&await db.ExecuteScalarAsync<string>("SELECT PBBOL FROM OPS_HEADER WHERE PB_ID=@id",new{id})=="BOL-TEST"&&
            await db.ExecuteScalarAsync<string>("SELECT PB_EXTERNAL_REF FROM OPS_HEADER WHERE PB_ID=@id",new{id})==$"aurora-sync:{request.Id:N}","creation maps terminal, PRO location and distinct business/audit references");
        check(await db.ExecuteScalarAsync<decimal>("SELECT PBTPIECE FROM OPS_HEADER WHERE PB_ID=@id",new{id})==2&&await db.ExecuteScalarAsync<decimal>("SELECT PB_TTL_CUBE FROM OPS_HEADER WHERE PB_ID=@id",new{id})==1.5m,
            "creation derives native header totals from seeded lines");
        check(await db.ExecuteScalarAsync<DateTime>("SELECT OPS_APPT_LOW FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND OPS_TYPE='D'",new{id})==new DateTime(2026,10,28,10,0,0),"creation stores delivery schedule in native local time");
        check(await db.ExecuteScalarAsync<string>("SELECT OPS_NAME FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND OPS_TYPE='P'",new{id})=="Sender"&&await db.ExecuteScalarAsync<string>("SELECT PBBNAME FROM OPS_HEADER WHERE PB_ID=@id",new{id})=="Payer","shipper, consignee and bill-to are native records");
        var batch=await provider.Pull(null,default);check(batch.Changes.Single().ExternalId==id.ToString()&&!batch.Changes.Single().Deleted,"new order emits one normalization refresh");
        var baseline=batch.Changes.Single().Data;await provider.Acknowledge(batch,default);
        await Reject(Data(),"existing PRO cannot be created under a different change ID");
        await Reject(Data("ABC"),"nonnumeric Nova PRO rejected");await Reject(Data("000123"),"ambiguous zero-prefixed PRO rejected");
        var active=Data("900009");active["Status"]="InTransit";await Reject(active,"creation cannot manufacture an active execution state");
        var wrongTerminal=Data("900009");wrongTerminal["TerminalCode"]="UNKNOWN";await Reject(wrongTerminal,"creation rejects an unmapped terminal");
        var invalid=Data("900002");invalid["Pieces"]=99;await Reject(invalid,"invalid freight rolls back header and stops");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_ORDER_KEY WHERE PRO_NUMBER=900002")==0,"failed create releases its PRO reservation");
        var copied=Data("900003");copied["Lykes"]!["Lines"]![0]!["LineId"]=input["Lykes"]!["Lines"]![0]!["LineId"]!.DeepClone();await Reject(copied,"order creation cannot steal an existing line identity");
        mapper.ReadyStatuses=[110,111];await Reject(Data("900004"),"multiple ready statuses require explicit creation choice");
        options.NewOrderReadyStatus=110;var selected=await provider.Push(Change(Data("900004")),default);
        check(await db.ExecuteScalarAsync<int>("SELECT PB_STATUS FROM OPS_HEADER WHERE PB_ID=@id",new{id=long.Parse(selected.ExternalId)})==110,"explicit ready status resolves creation ambiguity");
        options.NewOrderReadyStatus=null;mapper.ReadyStatuses=[110];
        mapper.FailAfter=true;await Reject(Data("900005"),"failure after complete creation rolls back all native rows");mapper.FailAfter=false;
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_ORDER_KEY WHERE PRO_NUMBER=900005")==0,"post-create rollback removes PRO reservation");
        // The cancellation mapper receives the last imported native baseline.
        mapper.CancelId=id;mapper.Baseline=baseline;
        JsonObject Cancellation(){var data=(JsonObject)baseline.DeepClone();data["FreightOpsState"]="Voided";data["CancellationReason"]="Customer cancelled pickup";return data;}
        options.OrderLifecycleEnabled=false;await Reject(Cancellation(),"cancellation also requires lifecycle opt-in","delete");options.OrderLifecycleEnabled=true;
        foreach(var field in new[]{"PB_DT_RATE","PB_DT_AUD","PB_DT_BILL","PB_INV_DATE"})
        {
            await db.ExecuteAsync($"UPDATE OPS_HEADER SET {field}=CURRENT_TIMESTAMP WHERE PB_ID=@id",new{id});await Reject(Cancellation(),$"{field} blocks cancellation even with zero revenue","delete");
            await db.ExecuteAsync($"UPDATE OPS_HEADER SET {field}=NULL WHERE PB_ID=@id",new{id});
        }
        await db.ExecuteAsync("UPDATE OPS_HEADER SET PBTOTPD=1 WHERE PB_ID=@id",new{id});await Reject(Cancellation(),"paid order requires native cancellation review","delete");await db.ExecuteAsync("UPDATE OPS_HEADER SET PBTOTPD=0 WHERE PB_ID=@id",new{id});
        await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_NO_CHRG='Y' WHERE PB_ID=@id",new{id});await Reject(Cancellation(),"native no-charge billing flag requires cancellation review","delete");await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_NO_CHRG=NULL WHERE PB_ID=@id",new{id});
        await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_RATE=1 WHERE OPL_ORDER_ID=@id",new{id});await Reject(Cancellation(),"rated lines cannot be cancelled through sync","delete");await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_RATE=0 WHERE OPL_ORDER_ID=@id",new{id});
        await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=20 WHERE OPS_ORDER_ID=@id AND OPS_TYPE='P'",new{id});await Reject(Cancellation(),"manifested order cannot be cancelled","delete");await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=NULL WHERE OPS_ORDER_ID=@id",new{id});
        var stop=await db.ExecuteScalarAsync<long>("SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND OPS_TYPE='P'",new{id});
        await db.ExecuteAsync("INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_PKUP_DELV_ID) VALUES(987654,@stop)",new{stop});await Reject(Cancellation(),"actual membership blocks cancellation despite blank stop manifest field","delete");await db.ExecuteAsync("DELETE FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=987654");
        var edited=Cancellation();edited["Lykes"]!["Lines"]![0]!["Pieces"]=3;await Reject(edited,"cancellation cannot hide a freight edit","delete");
        await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_PIECES=3 WHERE OPL_ORDER_ID=@id",new{id});await Reject(Cancellation(),"stale native freight conflicts before cancellation","delete");await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_PIECES=2 WHERE OPL_ORDER_ID=@id",new{id});
        await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(998,'VOID')");await Reject(Cancellation(),"ambiguous native VOID status requires review","delete");await db.ExecuteAsync("DELETE FROM CT_STAT WHERE CTS_ID=998");
        mapper.FailAfter=true;await Reject(Cancellation(),"failure after cancellation rolls back status and receipt","delete");mapper.FailAfter=false;
        check(await db.ExecuteScalarAsync<int>("SELECT PB_STATUS FROM OPS_HEADER WHERE PB_ID=@id",new{id})==110&&await db.ExecuteScalarAsync<DateTime?>("SELECT PB_DT_CANC FROM OPS_HEADER WHERE PB_ID=@id",new{id}) is null,"failed cancellation restores active native status");
        await db.ExecuteAsync("DELETE FROM AURORA_SYNC_EVENT");
        var cancel=Change(Cancellation(),"delete");var cancelled=await provider.Push(cancel,default);calls=mapper.Calls;
        check(cancelled==await provider.Push(cancel,default)&&mapper.Calls==calls,"cancellation retry does not repeat native voiding");
        check(await db.ExecuteScalarAsync<int>("SELECT PB_STATUS FROM OPS_HEADER WHERE PB_ID=@id",new{id})==999&&await db.ExecuteScalarAsync<string>("SELECT PBCINIT FROM OPS_HEADER WHERE PB_ID=@id",new{id})=="AURTEST"&&
            (await db.ExecuteScalarAsync<string>("SELECT PB_RATE_NOTES FROM OPS_HEADER WHERE PB_ID=@id",new{id}))!.Contains(cancel.Id.ToString()),"native VOID state carries configured actor and reason/change audit");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@id",new{id})==1&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_ORDER_KEY WHERE ORDER_ID=@id",new{id})==1,
            "cancellation retains native lines and PRO reservation");
        var tombstone=await provider.Pull(null,default);check(tombstone.Changes.Single().Deleted,"native cancellation returns a sync tombstone");await provider.Acknowledge(tombstone,default);
        var cancelledAt=await db.ExecuteScalarAsync<DateTime>("SELECT PB_DT_CANC FROM OPS_HEADER WHERE PB_ID=@id",new{id});await provider.Push(Change(Cancellation(),"delete"),default);
        check(await db.ExecuteScalarAsync<DateTime>("SELECT PB_DT_CANC FROM OPS_HEADER WHERE PB_ID=@id",new{id})==cancelledAt&&(await provider.Pull(null,default)).Changes.Count==0,"already-cancelled order preserves original audit without another event");
        mapper.CancelId=null;await Reject(Data(),"cancelled PRO cannot be reused for a new order");
        async Task<bool> Race(){try{await provider.Push(Change(Data("900006")),default);return true;}catch(SyncConflictException){return false;}}
        check((await Task.WhenAll(Race(),Race())).Count(x=>x)==1,"concurrent creates of one PRO have exactly one winner");
        var otherConsumer=Guid.NewGuid();await db.ExecuteAsync("INSERT INTO AURORA_SYNC_CONSUMER(CONSUMER_ID,ENABLED) VALUES(@id,'Y')",new{id=otherConsumer.ToString()});
        var otherProvider=new NovaSyncProvider(otherConsumer,cs,new Mapper(options,zone));
        async Task<bool> CrossRace(NovaSyncProvider target,Guid connection)
        {
            try{await target.Push(Change(Data("900007")) with{ConnectionId=connection},default);return true;}
            catch(SyncConflictException){return false;}
        }
        check((await Task.WhenAll(CrossRace(provider,consumer),CrossRace(otherProvider,otherConsumer))).Count(x=>x)==1&&
            await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PBNUM=900007")==1,"PRO reservation protects concurrent independent sync connections");
    }

    sealed class Mapper(LykesNovaOptions options,TimeZoneInfo zone):INovaSyncMapper
    {
        public long? CancelId;public JsonObject? Baseline;public int[] ReadyStatuses=[110];public bool FailAfter;public int Calls;
        readonly Dictionary<long,JsonObject> created=new();
        public async Task<ExternalReceipt> Push(SyncChange change,FbConnection db,FbTransaction tx,CancellationToken ct)
        {
            Calls++;var data=JsonNode.Parse(change.Payload)!.AsObject();long id;
            if(change.Operation=="delete")
            {
                id=CancelId!.Value;await LykesNovaOrderWriter.Cancel(db,tx,id,data,Baseline!,options,ReadyStatuses,change.ConnectionId,change.Id,zone,ct);
            }
            else {id=await LykesNovaOrderWriter.Create(db,tx,data,options,ReadyStatuses,change.ConnectionId,change.Id,zone,ct);created[id]=data;}
            if(FailAfter)throw new FormatException("Injected failure after order lifecycle mutation.");
            return new(id.ToString(),change.Id.ToString());
        }
        public async Task<IReadOnlyList<NovaSyncSnapshot>> Read(NovaChangeEvent change,FbConnection db,FbTransaction tx,CancellationToken ct)
        {
            var id=long.Parse(change.EntityId);var data=(JsonObject)(id==CancelId?Baseline!:created[id]).DeepClone();data["NativeOrderId"]=id;
            var deleted=await db.ExecuteScalarAsync<DateTime?>(new CommandDefinition("SELECT PB_DT_CANC FROM OPS_HEADER WHERE PB_ID=@id",new{id},tx,cancellationToken:ct)) is not null;
            if(!deleted){data["TerminalCode"]="N-AUS";data=LykesNovaMapping.Project(data,await LykesNovaMapping.ReadLines(db,tx,id,ct),options);}
            return [new("order",id.ToString(),Guid.NewGuid().ToString(),data,deleted){Open=true}];
        }
    }
}
