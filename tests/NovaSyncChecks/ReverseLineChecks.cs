using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Text.Json.Nodes;

static class ReverseLineChecks
{
    public static async Task Run(string cs,FbConnection db,Guid consumer,Action<bool,string> check)
    {
        foreach(var column in new[]{"PB_DT_RATE TIMESTAMP","PB_DT_AUD TIMESTAMP","PB_DT_BILL DATE","PB_INV_DATE DATE","PBTOTPD DECIMAL(14,4)","PBILPAY DECIMAL(14,4)","PBILREC DECIMAL(14,4)","PB_DISCOUNT_AMT DECIMAL(14,4)","PB_DISCOUNT_PERC DECIMAL(14,4)","PB_COD_AMT DECIMAL(14,4)","PB_MASTER_ID BIGINT","PB_NO_CHRG CHAR(1)"})await db.ExecuteAsync("ALTER TABLE OPS_HEADER ADD "+column);
        foreach(var column in new[]{"PB_TYPE CHAR(1)","PB_STATUS INTEGER","PB_DT_CANC TIMESTAMP","PBTOTBIL DECIMAL(14,4)","PBCREV DECIMAL(14,4)","PB_LOCATION VARCHAR(20)","PBTPIECE DECIMAL(14,4)","PBTWEIGH DECIMAL(14,4)","PB_TTL_CUBE DECIMAL(14,4)"})await db.ExecuteAsync("ALTER TABLE OPS_HEADER ADD "+column);
        foreach(var column in new[]{"OPS_RATE SMALLINT","OPS_MANIFEST_NUMBER BIGINT","OPS_DELIVERED_TIME TIMESTAMP","OPS_TTL_PIECES DECIMAL(14,4)","OPS_TTL_WEIGHT DECIMAL(14,4)","OPS_TTL_CUBE DECIMAL(14,4)"})await db.ExecuteAsync("ALTER TABLE OPS_STOP_REC ADD "+column);
        foreach(var column in new[]{"OPL_RATE DECIMAL(14,4)","OPL_DIM_WGT DECIMAL(14,4)","OPL_TONS DECIMAL(14,4)","OPL_MET_TONS DECIMAL(14,4)"})await db.ExecuteAsync("ALTER TABLE OPS_LINE_ITEMS ADD "+column);
        await db.ExecuteAsync("CREATE TABLE KEYWORDB(KW_ID VARCHAR(20) PRIMARY KEY,KW_TOT_PIECES CHAR(1),KW_TOT_WEIGHT CHAR(1))");
        await db.ExecuteAsync("ALTER TABLE OPS_HEADER ADD PB_RATE_NOTES VARCHAR(100)");
        await db.ExecuteAsync("ALTER TABLE OPS_LINE_ITEMS ADD OPL_RATE_TYPE VARCHAR(10)");
        await db.ExecuteAsync("INSERT INTO KEYWORDB VALUES('FRT','Y','Y')");
        await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_STATUS,PB_LOCATION,PBTPIECE,PBTWEIGH,PB_TTL_CUBE) VALUES(800,'O',110,'N-AUS',5,100,7.5)");
        await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_TYPE,OPS_RATE,OPS_TTL_PIECES,OPS_TTL_WEIGHT,OPS_TTL_CUBE) VALUES(8001,800,'P',1,5,100,7.5)");
        await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_TYPE,OPS_RATE) VALUES(8002,800,'D',0)");
        var first=Guid.NewGuid();var second=Guid.NewGuid();var charge=Guid.NewGuid();
        foreach(var (id,sequence,keyword,sku,pieces,weight,cube) in new[]{(first,1,"FRT","A",3,30,"2.5"),(second,2,"FRT","B",2,70,"5"),(charge,3,"FUEL","",0,0,"")})
            await db.ExecuteAsync("""
                INSERT INTO OPS_LINE_ITEMS(OPL_ORDER_ID,OPL_SEQUENCE,OPL_STOP_ID,OPL_SYNC_ID,OPL_KEYWORD,OPL_SKU,OPL_UOM,
                  OPL_PIECES,OPL_WEIGHT,OPL_KILOS,OPL_CUBE,OPL_DESC,OPL_PALLETS)
                VALUES(800,@sequence,8001,@id,@keyword,@sku,'CTN',@pieces,@weight,0,@cube,'preserve description',1)
                """,new{id=id.ToString(),sequence,keyword,sku,pieces,weight,cube});
        var options=new LykesNovaOptions{Enabled=true,WritesEnabled=true,TerminalMappings=new(){{"N-AUS","AUS"}},KeywordKinds=new(){{"FRT","freight"},{"FUEL","charge"}},PackTypes=new(){{"CTN","C"}},CubeBasis="per-piece",ScalarCubeUnit="ft3",DimensionUnit="in",FreightStopKind="P"};
        async Task<JsonObject> Snapshot()
        {
            await using var tx=db.BeginTransaction();
            return LykesNovaMapping.Project(new(){["Id"]="800",["Status"]="Ready",["WorkType"]="Delivery",["TerminalCode"]="N-AUS"},await LykesNovaMapping.ReadLines(db,tx,800,default),options);
        }
        var mapper=new Mapper(options,await Snapshot());var provider=new NovaSyncProvider(consumer,cs,mapper);
        SyncChange Change(JsonObject data)=>new(Guid.NewGuid(),consumer,"order","800",0,"upsert",data.ToJsonString(),1,Guid.NewGuid());
        JsonObject Edit(JsonObject prior,int pieces=4,decimal weight=40,decimal cube=3)
        {
            var data=(JsonObject)prior.DeepClone();var row=data["Lykes"]!["Lines"]![0]!;
            row["Pieces"]=pieces;row["TotalWeight"]=weight;row["CubePerPiece"]=cube;
            data["Pieces"]=pieces+2;data["Weight"]=weight+70;data["Cube"]=pieces*cube+10;
            // Real FreightOps reads add these optional empty fields.
            row["PO"]="";row["BOL"]="";row["Notes"]="";data["FreightOpsState"]="Created";data["FreightOpsParent"]=null;
            return data;
        }
        async Task Reject(JsonObject data,string label)
        {
            var receipts=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_RECEIPT");
            try{await provider.Push(Change(data),default);throw new Exception("Expected rejection: "+label);}
            catch(Exception e) when(e is FormatException or SyncConflictException){check(true,label);}
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_RECEIPT")==receipts,label+" leaves no receipt");
        }
        var request=Change(Edit(mapper.Baseline));
        await db.ExecuteAsync("DELETE FROM AURORA_SYNC_EVENT");
        var receipt=await provider.Push(request,default);
        check(receipt==await provider.Push(request,default)&&mapper.Calls==1,"physical write retry uses durable receipt without applying deltas twice");
        check(await db.ExecuteScalarAsync<decimal>("SELECT OPL_PIECES FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=800 AND OPL_SEQUENCE=1")==4,
            "reverse update finds the existing line by stable ID");
        check(await db.ExecuteScalarAsync<decimal>("SELECT PBTPIECE FROM OPS_HEADER WHERE PB_ID=800")==6&&await db.ExecuteScalarAsync<decimal>("SELECT PB_TTL_CUBE FROM OPS_HEADER WHERE PB_ID=800")==8,
            "native header totals follow Nova scalar convention rather than FreightOps extended cube");
        check(await db.ExecuteScalarAsync<decimal>("SELECT OPS_TTL_WEIGHT FROM OPS_STOP_REC WHERE OPS_STOP_ID=8001")==110&&await db.ExecuteScalarAsync<decimal>("SELECT OPS_TTL_CUBE FROM OPS_STOP_REC WHERE OPS_STOP_ID=8001")==8,
            "stop totals change atomically with line values");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=800 AND OPL_DESC='preserve description' AND OPL_PALLETS=1")==3,
            "physical corrections preserve descriptions, pallets, charges and row identities");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@id",new{id=consumer.ToString()})==1,
            "reverse line write queues one normalization refresh, including after receipt retry");
        var refresh=await provider.Pull(null,default);
        check(refresh.Changes.Single().Data["Lykes"]!["Lines"]![0]!["Nova"]!["Pieces"]!.GetValue<decimal>()==4,
            "normalization refresh carries updated native metadata for the next baseline");
        await provider.Acknowledge(refresh,default);
        check((await provider.Pull(null,default)).Changes.Count==0,"acknowledged normalization refresh does not loop");
        check((await Snapshot())["Cube"]!.GetValue<decimal>()==22,"reverse write reimports as correct total physical cubic feet");
        await Reject(Edit(mapper.Baseline,5),"stale native baseline conflicts instead of overwriting");
        mapper.Baseline=await Snapshot();
        var queuedBefore=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@id",new{id=consumer.ToString()});
        await provider.Push(Change(mapper.Baseline),default);
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@id",new{id=consumer.ToString()})==queuedBefore,
            "unchanged physical snapshot creates no normalization event or native rewrite");
        var missing=Edit(mapper.Baseline);missing["Lykes"]!["Lines"]!.AsArray().RemoveAt(2);await Reject(missing,"omitted charge is not silently deleted");
        var bad=Edit(mapper.Baseline);bad["Lykes"]!["Lines"]![0]!["LineId"]=Guid.NewGuid().ToString();await Reject(bad,"new line IDs require explicit native creation");
        bad=Edit(mapper.Baseline);bad["Lykes"]!["Lines"]![0]!["Sku"]="OTHER";await Reject(bad,"SKU identity cannot change during physical correction");
        bad=Edit(mapper.Baseline);bad["Lykes"]!["Lines"]![0]!["Nova"]!["Amount"]=99;await Reject(bad,"raw native metadata cannot overwrite commercial data");
        bad=Edit(mapper.Baseline);bad["Pieces"]=100;await Reject(bad,"inconsistent header totals rejected");
        bad=Edit(mapper.Baseline,5,40.12345m);await Reject(bad,"precision loss is rejected rather than acknowledged as a rounded value");
        bad=Edit(mapper.Baseline);bad["Lykes"]!["Lines"]![0]!["Pieces"]="four";await Reject(bad,"malformed numeric line input is rejected");
        bad=Edit(mapper.Baseline);bad["TerminalCode"]="DFW";await Reject(bad,"terminal moves are not silently acknowledged by line writer");
        bad=Edit(mapper.Baseline);bad["FreightOpsState"]="Delivered";await Reject(bad,"handled FreightOps freight cannot change Nova measurements");
        await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=12 WHERE OPS_STOP_ID=8001");await Reject(Edit(mapper.Baseline,5),"assigned Nova freight held");
        await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=NULL WHERE OPS_STOP_ID=8001");
        await db.ExecuteAsync("INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_PKUP_DELV_ID) VALUES(8009,8001)");
        await Reject(Edit(mapper.Baseline,5),"actual manifest membership blocks corrections even with blank stop manifest");
        await db.ExecuteAsync("DELETE FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=8009");
        foreach(var column in new[]{"PB_DT_RATE","PB_DT_AUD","PB_DT_BILL","PB_INV_DATE","PBTOTPD","PBILPAY","PBILREC","PB_DISCOUNT_AMT","PB_DISCOUNT_PERC","PB_COD_AMT","PB_MASTER_ID","PB_NO_CHRG"})
        {
            var value=column=="PB_NO_CHRG"?"'Y'":column is "PB_DT_RATE" or "PB_DT_AUD" or "PB_DT_BILL" or "PB_INV_DATE"?"CURRENT_TIMESTAMP":"1";
            await db.ExecuteAsync($"UPDATE OPS_HEADER SET {column}={value} WHERE PB_ID=800");
            await Reject(Edit(mapper.Baseline,5),column+" blocks physical corrections even at zero price");
            await db.ExecuteAsync($"UPDATE OPS_HEADER SET {column}=NULL WHERE PB_ID=800");
        }
        await db.ExecuteAsync("UPDATE OPS_HEADER SET PBTOTBIL=50 WHERE PB_ID=800");await Reject(Edit(mapper.Baseline,5),"rated Nova order held without changing prices");
        await db.ExecuteAsync("UPDATE OPS_HEADER SET PBTOTBIL=0 WHERE PB_ID=800");
        await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_RATE=5 WHERE OPL_ORDER_ID=800 AND OPL_SEQUENCE=2");await Reject(Edit(mapper.Baseline,5),"another line's rating prevents stale commercial totals");
        await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_RATE=0 WHERE OPL_ORDER_ID=800");
        await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_RATE_NOTES='Rated zero-dollar shipment' WHERE PB_ID=800");await Reject(Edit(mapper.Baseline,5),"zero-dollar rated shipment also requires native reconciliation");
        await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_RATE_NOTES=NULL WHERE PB_ID=800");
        await db.ExecuteAsync("UPDATE KEYWORDB SET KW_TOT_PIECES='N' WHERE KW_ID='FRT'");await Reject(Edit(mapper.Baseline,5),"nonstandard native keyword totals are held");
        await db.ExecuteAsync("UPDATE KEYWORDB SET KW_TOT_PIECES='Y' WHERE KW_ID='FRT'");
        mapper.FailAfter=true;
        await Reject(Edit(mapper.Baseline,5,50,4),"failure after native writes rolls back business changes and receipt");mapper.FailAfter=false;
        check(await db.ExecuteScalarAsync<decimal>("SELECT PBTPIECE FROM OPS_HEADER WHERE PB_ID=800")==6&&await db.ExecuteScalarAsync<decimal>("SELECT OPL_WEIGHT FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=800 AND OPL_SEQUENCE=1")==40,
            "transaction rollback restores header and line together");
        var stale=mapper.Baseline;
        async Task<bool> Race(int pieces){try{await provider.Push(Change(Edit(stale,pieces)),default);return true;}catch(SyncConflictException){return false;}}
        var outcomes=await Task.WhenAll(Race(5),Race(6));check(outcomes.Count(x=>x)==1,"concurrent physical corrections have one winner and one baseline conflict");
        mapper.Baseline=await Snapshot();
        options.WritesEnabled=false;await Reject(Edit(mapper.Baseline,7),"reverse write opt-in remains off unless explicitly enabled");options.WritesEnabled=true;
        await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_CUBE='12x12x12' WHERE OPL_ORDER_ID=800 AND OPL_SEQUENCE=1");
        mapper.Baseline=await Snapshot();await Reject(Edit(mapper.Baseline,7),"dimension strings are preserved for native rating review");
        await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_CUBE='15' WHERE OPL_ORDER_ID=800 AND OPL_SEQUENCE=1");
        options.CubeBasis="line-total";
        mapper.Baseline=await Snapshot();
        var totalEdit=Edit(mapper.Baseline,7,70,3);totalEdit["Cube"]=26; // second native row is five total cubic feet
        await provider.Push(Change(totalEdit),default);
        check(await db.ExecuteScalarAsync<string>("SELECT OPL_CUBE FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=800 AND OPL_SEQUENCE=1")=="21",
            "explicit line-total Nova convention stores per-piece cube multiplied by pieces");
        await Lifecycle();

        async Task Lifecycle()
        {
            JsonObject Copy()=> (JsonObject)mapper.Baseline.DeepClone();
            JsonArray Rows(JsonObject data)=>data["Lykes"]!["Lines"]!.AsArray();
            void Totals(JsonObject data)
            {
                var freight=Rows(data).OfType<JsonObject>().Where(x=>x["Kind"]!.GetValue<string>()=="freight").ToArray();
                decimal N(JsonObject row,string key)=>System.Text.Json.JsonSerializer.Deserialize<decimal>(row[key]!.ToJsonString());
                data["Pieces"]=freight.Sum(x=>N(x,"Pieces"));data["Weight"]=freight.Sum(x=>N(x,"TotalWeight"));
                data["Cube"]=freight.Sum(x=>N(x,"Pieces")*N(x,"CubePerPiece"));
            }
            JsonObject Add(JsonObject data,Guid id,string sku="C")
            {
                Rows(data).Add(new JsonObject{["LineId"]=id.ToString(),["Kind"]="freight",["Sku"]=sku,["PackType"]="C",["Pieces"]=2,["TotalWeight"]=20,["CubePerPiece"]=1.5m});
                Totals(data);return data;
            }
            JsonObject Remove(JsonObject data,Guid id)
            {
                var row=Rows(data).OfType<JsonObject>().Single(x=>Guid.Parse(x["LineId"]!.GetValue<string>())==id);
                Rows(data).Remove(row);Totals(data);return data;
            }
            mapper.Baseline=await Snapshot();
            var addedId=Guid.NewGuid();
            await Reject(Add(Copy(),addedId),"addition requires explicit native freight keyword");
            options.NewFreightKeyword="FRT";
            await Reject(Add(Copy(),addedId),"addition requires manual identity-retention migration");
            foreach(var sql in File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"LineTombstones.sql")).Split(';',StringSplitOptions.RemoveEmptyEntries))
                if(sql.Split('\n').Any(x=>!string.IsNullOrWhiteSpace(x)&&!x.TrimStart().StartsWith("--")))await db.ExecuteAsync(sql);
            await db.ExecuteAsync("ALTER TABLE OPS_HEADER ADD PB_TTL_PALLETS INTEGER");
            await db.ExecuteAsync("ALTER TABLE OPS_HEADER ADD PB_TTL_PALLET_POSN INTEGER");
            await db.ExecuteAsync("ALTER TABLE OPS_STOP_REC ADD OPS_TTL_PALLETS INTEGER");
            await db.ExecuteAsync("ALTER TABLE OPS_STOP_REC ADD OPS_TTL_PALLET_POSN INTEGER");
            await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_TTL_PALLETS=3,PB_TTL_PALLET_POSN=9 WHERE PB_ID=800");
            await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_TTL_PALLETS=3,OPS_TTL_PALLET_POSN=9 WHERE OPS_STOP_ID=8001");
            await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_PALLET_POSITIONS=OPL_SEQUENCE+1 WHERE OPL_ORDER_ID=800");
            mapper.Baseline=await Snapshot();
            var addition=Add(Copy(),addedId);var addChange=Change(addition);
            var addReceipt=await provider.Push(addChange,default);
            check(addReceipt==await provider.Push(addChange,default),"added line retry returns original receipt");
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@id",new{id=addedId.ToString()})==1&&
                  await db.ExecuteScalarAsync<int>("SELECT OPL_SEQUENCE FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@id",new{id=addedId.ToString()})==4,
                "new freight appends once without resequencing existing IDs");
            check(await db.ExecuteScalarAsync<string>("SELECT OPL_UOM FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@id",new{id=addedId.ToString()})=="CTN"&&
                  await db.ExecuteScalarAsync<string>("SELECT OPL_CUBE FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@id",new{id=addedId.ToString()})=="3",
                "addition maps packaging and line-total cube explicitly");
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_LINE_TOMBSTONE WHERE LINE_ID=@id AND DELETED_AT IS NULL",new{id=addedId.ToString()})==1,
                "new identity reserved atomically with native line");
            await Reject(Add(Copy(),Guid.NewGuid(),"D"),"stale addition snapshot cannot overwrite new native line");
            mapper.Baseline=await Snapshot();
            await Reject(Add(Copy(),Guid.Empty,"EMPTY"),"empty added identity rejected");
            var foreignId=Guid.Parse(await db.ExecuteScalarAsync<string>("SELECT OPL_SYNC_ID FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=500")??throw new Exception("Missing fixture identity"));
            await Reject(Add(Copy(),foreignId,"OTHER-ORDER"),"another order's UUID cannot be adopted as a new line");
            await Reject(Add(Copy(),Guid.NewGuid(),"A"),"new line cannot reuse existing freight SKU");
            options.PackTypes["BOX"]="C";await Reject(Add(Copy(),Guid.NewGuid(),"AMBIGUOUS"),"ambiguous reverse packaging held");options.PackTypes.Remove("BOX");
            var forged=Add(Copy(),Guid.NewGuid(),"FORGED");Rows(forged).Last()!["Nova"]=new JsonObject();await Reject(forged,"added line cannot forge native metadata");
            var newCharge=Add(Copy(),Guid.NewGuid(),"CHARGE");Rows(newCharge).Last()!["Kind"]="charge";await Reject(newCharge,"new charge cannot bypass freight safeguards");
            foreach(var guard in new[]{"PB_INV_DATE=CURRENT_DATE","PB_NO_CHRG='Y'","PB_MASTER_ID=99"})
            {
                await db.ExecuteAsync($"UPDATE OPS_HEADER SET {guard} WHERE PB_ID=800");
                await Reject(Add(Copy(),Guid.NewGuid(),"PROTECTED"),guard+" blocks additions");
                await Reject(Remove(Copy(),second),guard+" blocks deletions");
                await db.ExecuteAsync($"UPDATE OPS_HEADER SET {guard.Split('=')[0]}=NULL WHERE PB_ID=800");
            }
            await db.ExecuteAsync("INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_PKUP_DELV_ID) VALUES(8009,8001)");
            await Reject(Add(Copy(),Guid.NewGuid(),"MANIFESTED"),"actual manifest membership blocks additions");
            await Reject(Remove(Copy(),second),"actual manifest membership blocks deletions");
            await db.ExecuteAsync("DELETE FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=8009");
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=800")==4&&
                  await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@id",new{id=second.ToString()})==1,
                "protected addition/deletion attempts retain all native lines");
            await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_RATE_NOTES='rated' WHERE PB_ID=800");
            await Reject(Add(Copy(),Guid.NewGuid(),"RATED"),"rating blocks additions");
            await Reject(Remove(Copy(),second),"rating blocks deletions before any native mutation");
            await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_RATE_NOTES=NULL WHERE PB_ID=800");
            await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_PALLET_POSITIONS=4 WHERE OPL_ORDER_ID=800 AND OPL_SEQUENCE=2");
            await Reject(Remove(Copy(),second),"changed native pallet positions conflict before deletion");
            await db.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_PALLET_POSITIONS=3 WHERE OPL_ORDER_ID=800 AND OPL_SEQUENCE=2");
            await Reject(Remove(Remove(Remove(Copy(),first),second),addedId),"deleting the last freight line requires cancellation review");
            await Reject(Remove(Copy(),charge),"charge deletion is never inferred from omission");
            var rollbackId=Guid.NewGuid();var mixed=Add(Remove(Copy(),second),rollbackId,"ROLLBACK");
            var beforeRollback=(await db.ExecuteScalarAsync<decimal>("SELECT PBTPIECE FROM OPS_HEADER WHERE PB_ID=800"),await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT"));
            mapper.FailAfter=true;await Reject(mixed,"mixed addition/deletion failure rolls back");mapper.FailAfter=false;
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@id",new{id=second.ToString()})==1&&
                  await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_LINE_TOMBSTONE WHERE LINE_ID IN (@oldId,@newId)",new{oldId=second.ToString(),newId=rollbackId.ToString()})==0,
                "rollback restores deleted line and removes both identity records");
            check(beforeRollback==(await db.ExecuteScalarAsync<decimal>("SELECT PBTPIECE FROM OPS_HEADER WHERE PB_ID=800"),await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT")),
                "mixed rollback also restores totals and notification queue");
            var beforePcs=await db.ExecuteScalarAsync<decimal>("SELECT PBTPIECE FROM OPS_HEADER WHERE PB_ID=800");
            var deletion=Change(Remove(Copy(),second));var deletedReceipt=await provider.Push(deletion,default);
            check(deletedReceipt==await provider.Push(deletion,default)&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@id",new{id=second.ToString()})==0,
                "native deletion is stable-ID scoped and retry-safe");
            check(await db.ExecuteScalarAsync<decimal>("SELECT PBTPIECE FROM OPS_HEADER WHERE PB_ID=800")==beforePcs-2&&
                  await db.ExecuteScalarAsync<int>("SELECT PB_TTL_PALLETS FROM OPS_HEADER WHERE PB_ID=800")==2&&
                  await db.ExecuteScalarAsync<int>("SELECT OPS_TTL_PALLET_POSN FROM OPS_STOP_REC WHERE OPS_STOP_ID=8001")==6,
                "deletion subtracts pieces, pallets and pallet positions from native totals");
            mapper.Baseline=await Snapshot();
            await Reject(Add(Copy(),second,"REUSE"),"retired identity cannot be resurrected");
            var deleteAdded=Change(Remove(Copy(),addedId));await provider.Push(deleteAdded,default);
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_LINE_TOMBSTONE WHERE LINE_ID=@id AND DELETED_AT IS NOT NULL",new{id=addedId.ToString()})==1,
                "deleting a sync-created line retires its existing reservation");
            mapper.Baseline=await Snapshot();
            await Reject(Add(Copy(),addedId,"REUSED-ADD"),"deleted sync-created identity remains unavailable");
            var raceBaseline=Copy();
            async Task<bool> AddRace(string sku)
            {
                try{await provider.Push(Change(Add((JsonObject)raceBaseline.DeepClone(),Guid.NewGuid(),sku)),default);return true;}
                catch(SyncConflictException){return false;}
            }
            var winners=await Task.WhenAll(AddRace("RACE-A"),AddRace("RACE-B"));
            check(winners.Count(x=>x)==1,"concurrent additions cannot allocate from a stale full snapshot");
        }
    }

    sealed class Mapper(LykesNovaOptions options,JsonObject baseline):INovaSyncMapper
    {
        public JsonObject Baseline=baseline;public bool FailAfter;public int Calls;
        public async Task<ExternalReceipt> Push(SyncChange change,FbConnection db,FbTransaction tx,CancellationToken ct)
        {
            Calls++;await LykesNovaLineWriter.Apply(db,tx,800,JsonNode.Parse(change.Payload)!.AsObject(),Baseline,options,[110],change.ConnectionId,ct);
            if(FailAfter)throw new FormatException("Injected failure after physical updates.");
            return new("800",change.Id.ToString());
        }
        public async Task<IReadOnlyList<NovaSyncSnapshot>> Read(NovaChangeEvent change,FbConnection db,FbTransaction tx,CancellationToken ct)
        {
            var source=(JsonObject)Baseline.DeepClone();source["TerminalCode"]="N-AUS";
            var data=LykesNovaMapping.Project(source,await LykesNovaMapping.ReadLines(db,tx,800,ct),options);
            var revision=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(data.ToJsonString())));
            return [new("order","800",revision,data){Open=true}];
        }
    }
}
