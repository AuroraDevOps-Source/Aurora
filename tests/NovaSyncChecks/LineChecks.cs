using Aurora.Integrations.Nova.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Text.Json.Nodes;

static class LineChecks
{
    public static async Task Run(FbConnection db,Guid consumer,Action<bool,string> check)
    {
        void Reject(Action action,string label){try{action();}catch(FormatException){check(true,label);return;}throw new Exception("Expected rejection: "+label);}
        await db.ExecuteAsync("""
            CREATE TABLE OPS_LINE_ITEMS(OPL_ORDER_ID BIGINT NOT NULL,OPL_SEQUENCE INTEGER NOT NULL,
              OPL_STOP_ID BIGINT,OPL_KEYWORD VARCHAR(20),OPL_SKU VARCHAR(30),OPL_DESC VARCHAR(80),
              OPL_UOM VARCHAR(20),OPL_CUBE VARCHAR(40),OPL_PIECES DECIMAL(12,4),
              OPL_WEIGHT DECIMAL(12,4),OPL_KILOS DECIMAL(12,4),OPL_PALLETS INTEGER,OPL_PALLET_POSITIONS INTEGER,OPL_AMOUNT DECIMAL(12,4),
              PRIMARY KEY(OPL_ORDER_ID,OPL_SEQUENCE))
            """);
        await db.ExecuteAsync("ALTER TABLE OPS_STOP_REC ADD OPS_TYPE CHAR(1)");
        await using(var tx=db.BeginTransaction())
        {
            check((await LykesNovaMapping.ReadLines(db,tx,500,default,nativeReadOnly:true)).Length==0,"MVP reader works on unmodified Nova schema without stable-ID column");
            try{await LykesNovaMapping.ReadLines(db,tx,500,default);throw new Exception("Expected missing schema hold");}
            catch(FormatException){check(true,"Lykes reader holds old schema without changing it");}
        }
        foreach(var statement in File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"StableLineIds.sql")).Split(';',StringSplitOptions.RemoveEmptyEntries))
            if(statement.Split('\n').Any(l=>!string.IsNullOrWhiteSpace(l)&&!l.TrimStart().StartsWith("--")))await db.ExecuteAsync(statement);
        var script=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"LineChangeCapture.sql"));
        foreach(var statement in script.Split("SET TERM ^;")[1].Split("SET TERM ;^")[0].Split('^',StringSplitOptions.RemoveEmptyEntries).Where(x=>!string.IsNullOrWhiteSpace(x)))await db.ExecuteAsync(statement);
        await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_TYPE) VALUES(5001,500,'P')");
        await db.ExecuteAsync("INSERT INTO OPS_LINE_ITEMS(OPL_ORDER_ID,OPL_SEQUENCE,OPL_STOP_ID) VALUES(501,1,5001)");
        await db.ExecuteAsync("INSERT INTO OPS_LINE_ITEMS(OPL_ORDER_ID,OPL_SEQUENCE,OPL_STOP_ID) VALUES(501,2,5001)");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID IS NULL")==2,"legacy inserts remain compatible and are not assigned misleading IDs");
        var id=Guid.NewGuid().ToString();
        await db.ExecuteAsync("""
            INSERT INTO OPS_LINE_ITEMS(OPL_ORDER_ID,OPL_SEQUENCE,OPL_STOP_ID,OPL_SYNC_ID,OPL_KEYWORD,OPL_SKU,
              OPL_UOM,OPL_CUBE,OPL_PIECES,OPL_WEIGHT,OPL_KILOS,OPL_PALLETS)
            VALUES(500,1,5001,@id,'FRT','SKU-1','CTN','2.5',3,30,0,1)
            """,new{id});
        try{await db.ExecuteAsync("INSERT INTO OPS_LINE_ITEMS(OPL_ORDER_ID,OPL_SEQUENCE,OPL_SYNC_ID) VALUES(502,1,@id)",new{id});throw new Exception("Expected duplicate rejection");}
        catch(FbException){check(true,"stable line identity cannot be reused by another order");}
        await db.ExecuteAsync("DELETE FROM AURORA_SYNC_EVENT");
        await using(var tx=db.BeginTransaction())
        {
            await db.ExecuteAsync("DELETE FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=500",transaction:tx);
            await tx.RollbackAsync();
        }
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT")==0,"line mutation rollback also rolls back notifications");
        await using(var tx=db.BeginTransaction())
        {
            await db.ExecuteAsync("DELETE FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=500",transaction:tx);
            await db.ExecuteAsync("""
                INSERT INTO OPS_LINE_ITEMS(OPL_ORDER_ID,OPL_SEQUENCE,OPL_STOP_ID,OPL_SYNC_ID,OPL_KEYWORD,OPL_SKU,
                  OPL_UOM,OPL_CUBE,OPL_PIECES,OPL_WEIGHT,OPL_KILOS,OPL_PALLETS)
                VALUES(500,7,5001,@id,'FRT','SKU-1','CTN','2.5',3,30,0,1)
                """,new{id},tx);
            await tx.CommitAsync();
        }
        NovaSyncLine[] rows;
        await using(var tx=db.BeginTransaction())rows=await LykesNovaMapping.ReadLines(db,tx,500,default);
        check(rows.Single().LineId==id&&rows[0].Sequence==7,"real Firebird delete/reinsert and resequence preserve supplied line ID");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@id AND ENTITY_ID='500'",new{id=consumer.ToString()})==2,"line deletion and insertion both capture owning order");
        var config=new LykesNovaOptions{Enabled=true,TerminalMappings=new(){{"N-AUS","AUS"},{"N-DFW","DFW"}},KeywordKinds=new(){{"FRT","freight"},{"FUEL","charge"}},PackTypes=new(){{"CTN","CARTON"}},CubeBasis="per-piece",ScalarCubeUnit="ft3",DimensionUnit="in",FreightStopKind="P"};
        var tenantId=Guid.NewGuid();
        var defaults=new Aurora.Integrations.Nova.FirebirdRoutingOptions{TenantId=tenantId,SourceKey="lykes-fixture",Lykes=config};
        check(defaults.LykesForSource(tenantId,"lykes-fixture").Enabled,"Lykes configuration resolves for its explicit tenant and native source");
        check(!defaults.LykesForSource(Guid.NewGuid(),"lykes-fixture").Enabled&&!defaults.LykesForSource(tenantId,"another-database").Enabled,
            "host mapping defaults cannot leak across tenants or native databases");
        var source=new JsonObject{["Id"]="LYKES-500",["TerminalCode"]="N-AUS",["Pieces"]=999,["Weight"]=999,["Cube"]=999};
        var mapped=LykesNovaMapping.Project(source,rows,config);
        check(mapped["TerminalCode"]!.GetValue<string>()=="AUS"&&mapped["Pieces"]!.GetValue<int>()==3&&mapped["Cube"]!.GetValue<decimal>()==7.5m,"Nova pieces and ft3 per piece project to Lykes totals, not pallet counts");
        check(mapped["Lykes"]!["Lines"]![0]!["LineId"]!.GetValue<string>()==id&&mapped["Weight"]!.GetValue<decimal>()==30,"native ID and pounds reach Lykes freight");
        check(source["Pieces"]!.GetValue<int>()==999&&!source.ContainsKey("Lykes"),"mapping does not mutate the input snapshot");
        source["TerminalCode"]="N-DFW";
        check(LykesNovaMapping.Project(source,rows,config)["TerminalCode"]!.GetValue<string>()=="DFW","multiple terminals resolve independently");
        var charge=new NovaSyncLine{LineId=Guid.NewGuid().ToString(),Keyword="FUEL",Amount=25};
        var charged=LykesNovaMapping.Project(source,[rows[0],charge],config);
        check(charged["Pieces"]!.GetValue<int>()==3&&charged["Lykes"]!["Lines"]!.AsArray().Count==2,"charges are retained without generating pieces");
        mapped["TerminalCode"]="N-AUS";mapped["Lykes"]!["Lines"]![0]!["PO"]="PO-existing";
        check(LykesNovaMapping.Project(mapped,rows,config)["Lykes"]!["Lines"]![0]!["PO"]!.GetValue<string>()=="PO-existing","source-unowned canonical line fields survive import");
        rows[0].Cube="12x12x12";
        check(Math.Abs(LykesNovaMapping.Project(source,rows,config)["Cube"]!.GetValue<decimal>()-3)<0.000001m,"inch dimensions become ft3 per piece");
        config.CubeBasis="line-total";rows[0].Cube="7.5";
        check(LykesNovaMapping.Project(source,rows,config)["Lykes"]!["Lines"]![0]!["CubePerPiece"]!.GetValue<decimal>()==2.5m,"explicit line-total cube divides by piece count");
        rows[0].Cube="12x12";Reject(()=>LykesNovaMapping.Project(source,rows,config),"area-only Nova cube is held");rows[0].Cube="7.5";
        rows[0].Pieces=1.5m;Reject(()=>LykesNovaMapping.Project(source,rows,config),"fractional pieces cannot create barcodes");rows[0].Pieces=3;
        rows[0].LineId=null;Reject(()=>LykesNovaMapping.Project(source,rows,config),"old writer missing ID requires adoption, not regenerated identity");rows[0].LineId=id;
        Reject(()=>LykesNovaMapping.Project(source,[rows[0],rows[0]],config),"duplicate source identities are held");
        rows[0].StopKind="D";Reject(()=>LykesNovaMapping.Project(source,rows,config),"freight at unconfirmed stop is not silently dropped or doubled");rows[0].StopKind="P";
        rows[0].Kilos=30;Reject(()=>LykesNovaMapping.Project(source,rows,config),"conflicting pounds and kilos are held");rows[0].Kilos=0;
        rows[0].Keyword="UNKNOWN";Reject(()=>LykesNovaMapping.Project(source,rows,config),"unknown freight versus charge classification is held");rows[0].Keyword="FRT";
        rows[0].Uom="UNKNOWN";Reject(()=>LykesNovaMapping.Project(source,rows,config),"unknown packaging is held");rows[0].Uom="CTN";
        source["TerminalCode"]="UNKNOWN";Reject(()=>LykesNovaMapping.Project(source,rows,config),"unmapped terminal cannot use a default");source["TerminalCode"]="N-AUS";
        config.TerminalMappings["N-DFW"]="AUS";Reject(()=>LykesNovaMapping.Project(source,rows,config),"ambiguous terminal crosswalk is rejected");config.TerminalMappings["N-DFW"]="DFW";
        config.CubeBasis="";Reject(()=>LykesNovaMapping.Project(source,rows,config),"unconfirmed Nova cube basis is held");
        Reject(()=>LykesNovaMapping.RequireSupportedWrite("order",source,config),"Lykes order writes cannot fall through to header-only writer");
        Reject(()=>LykesNovaMapping.RequireSupportedWrite("manifest",source,config),"Lykes manifest writes remain held");
        config.Enabled=false;
        Reject(()=>LykesNovaMapping.Project(mapped,[],config),"disabling Lykes cannot import stale lines through a legacy header-only snapshot");
        check(JsonNode.DeepEquals(source,LykesNovaMapping.Project(source,[],config)),"disabled Lykes mapping preserves legacy snapshot without requiring schema or configuration");
        LykesNovaMapping.RequireSupportedWrite("order",source,config);check(true,"ordinary legacy order write remains available");
        Reject(()=>LykesNovaMapping.RequireSupportedWrite("order",mapped,config),"Lykes payload cannot bypass write hold by disabling configuration");
        config.Enabled=true;config.NativeReadOnly=true;config.CubeBasis="per-piece";
        config.PilotOrderIds=[1862];
        check(config.IncludesEntity("order-or-manifest","1862"),"pilot includes selected native order");
        check(!config.IncludesEntity("order","1863")&&!config.IncludesEntity("manifest","1862"),"pilot excludes other orders and manifests");
        check(config.IncludesEntity("terminal","TPA"),"pilot retains reference data");
        check(!config.IncludesEntity("equipment","1")&&!config.IncludesEntity("driver","1"),"pilot uses local demo fleet without importing unrelated native equipment");
        config.PilotOrderIds=[];
        check(config.IncludesEntity("order","1863"),"empty pilot preserves source-wide behavior");
        rows[0].LineId=null;
        var mvp=LykesNovaMapping.Project(source,rows,config);
        var operationalId=mvp["Lykes"]!["Lines"]![0]!["LineId"]!.ToString();
        check(Guid.TryParse(operationalId,out _)&&rows[0].LineId is null,"MVP assigns operational identity without mutating native rows");
        mvp["TerminalCode"]="N-AUS";
        rows[0].Sequence=44;rows[0].StopId=999;rows[0].Amount=125;
        var rerated=LykesNovaMapping.Project(mvp,[rows[0],charge],config);
        check(rerated["Lykes"]!["Lines"]!.AsArray().Count==1&&rerated["Lykes"]!["Lines"]![0]!["LineId"]!.ToString()==operationalId,
            "MVP resequencing, row replacement and rating charges retain operational identity without exporting charges");
        rows[0].Weight+=1;Reject(()=>LykesNovaMapping.Project(mvp,rows,config),"MVP changed native freight is held for piece reconciliation");rows[0].Weight-=1;
        Reject(()=>LykesNovaMapping.Project(mvp,[rows[0],rows[0]],config),"adding a duplicate line to an existing snapshot still requires reconciliation");
        var blank=new NovaSyncLine{Keyword="FRT",StopKind="P",Sku="",Description="Shelving",Uom="CTN",Cube="1",Pieces=12,Weight=120,Pallets=1};
        var other=new NovaSyncLine{Keyword="FRT",StopKind="P",Sku="",Description="Racking",Uom="CTN",Cube="2",Pieces=2,Weight=40};
        var assumed=LykesNovaMapping.Project(source,[blank,other,charge],config);
        var assumedLines=assumed["Lykes"]!["Lines"]!.AsArray().OfType<JsonObject>().ToArray();
        check(assumedLines.Length==2&&assumedLines.Select(r=>r["Sku"]!.ToString()).Distinct().Count()==2&&
            assumedLines.All(r=>r["Sku"]!.ToString().StartsWith("AUR-")&&r["SkuGenerated"]!.GetValue<bool>()&&r["Nova"]!["Sku"]!.ToString()==""),
            "blank Nova SKUs receive distinct internal codes while native SKU values stay blank");
        check(assumedLines.Single(r=>r["Description"]!.ToString()=="Shelving")["Pieces"]!.GetValue<int>()==12&&assumed["Pieces"]!.GetValue<int>()==14,
            "twelve pieces on one pallet stay twelve pieces, not twelve lines or an extra pallet barcode");
        assumed["TerminalCode"]="N-AUS";blank.Sequence=77;blank.StopId=991;blank.Amount=200;
        var reordered=LykesNovaMapping.Project(assumed,[other,blank,charge],config);
        check(JsonNode.DeepEquals(assumed["Lykes"],reordered["Lykes"]),"internal codes and identities survive reordered rows and rating changes");
        check(blank.Sku==""&&blank.LineId is null,"generated codes never mutate Nova input rows");
        var repeated=LykesNovaMapping.Project(source,[rows[0],rows[0]],config)["Lykes"]!["Lines"]!.AsArray();
        check(repeated.Count==2&&repeated.Select(r=>r!["LineId"]!.ToString()).Distinct().Count()==2&&repeated.Select(r=>r!["Sku"]!.ToString()).Distinct().Count()==2,
            "identical repeated-SKU lines remain separate, each with its own identity");
        var realCode=new NovaSyncLine{Keyword="FRT",StopKind="P",Sku=assumedLines.Single(r=>r["Description"]!.ToString()=="Shelving")["Sku"]!.ToString(),Description="Real SKU collision",Uom="CTN",Cube="1",Pieces=1,Weight=1};
        var collision=LykesNovaMapping.Project(source,[blank,realCode],config)["Lykes"]!["Lines"]!.AsArray();
        check(collision.Select(r=>r!["Sku"]!.ToString()).Distinct().Count()==2&&collision.Any(r=>r!["Sku"]!.ToString()==realCode.Sku),
            "generated internal code cannot collide with a real source SKU");
        blank.Pieces=13;Reject(()=>LykesNovaMapping.Project(assumed,[blank,other],config),"physical changes still require reconciliation rather than replacing barcodes");blank.Pieces=12;
        Reject(()=>LykesNovaMapping.Project(mapped,rows,config),"MVP does not silently adopt earlier native identities");
        config.WritesEnabled=true;config.OrderLifecycleEnabled=true;
        Reject(()=>LykesNovaMapping.RequireSupportedWrite("order",mvp,config),"MVP rejects reverse orders even when legacy write flags are enabled");
        Reject(()=>LykesNovaMapping.RequireSupportedWrite("manifest",source,config),"MVP does not write manifests through synchronization");
        foreach(var kind in new[]{"terminal","customer","driver","equipment"})
            Reject(()=>LykesNovaMapping.RequireSupportedWrite(kind,source,config),$"MVP blocks reverse {kind} writes before native SQL");
        Reject(()=>LykesNovaOrderWriter.RequireEnabled(config),"MVP lifecycle remains blocked regardless of legacy flags");
        config.OperationalWritesEnabled=true;
        var scoped=new LykesNovaOptions{Enabled=true,PilotOrderIds=[100],ManifestSyncFromId=500};
        check(scoped.IncludesEntity("manifest","500")&&!scoped.IncludesEntity("manifest","499")&&
            !scoped.IncludesEntity("order","500")&&scoped.IncludesEntity("order","100")&&scoped.IncludesEntity("equipment","1"),
            "manifest rollout cutoff includes dependencies without accidentally enrolling new orders");
        LykesNovaMapping.RequireSupportedWrite("manifest",source,config);
        LykesNovaMapping.RequireSupportedWrite("order",mvp,config);
        check(true,"operational permission enables guarded order headers and manifests separately from physical freight");
        Reject(()=>LykesNovaOrderWriter.RequireEnabled(config),"operational permission cannot enable native freight lifecycle writes");
        foreach(var kind in new[]{"terminal","customer","driver","equipment"})
            Reject(()=>LykesNovaMapping.RequireSupportedWrite(kind,source,config),"operational permission does not grant master writes: "+kind);
        var operational=System.Text.Json.JsonSerializer.SerializeToNode(new Aurora.Contracts.TmsOrder{Id="123",Customer="Receiver"})!.AsObject();
        operational["Lykes"]=mvp["Lykes"]!.DeepClone();
        foreach(var field in new[]{"Id","Pieces","Weight","Cube","Status","WorkType","TerminalCode","Reference","BillTo"})
        {
            var edited=(JsonObject)operational.DeepClone();
            edited[field]=field is "Pieces" or "Weight" or "Cube"?System.Text.Json.Nodes.JsonValue.Create(5):System.Text.Json.Nodes.JsonValue.Create("changed");
            Reject(()=>LykesNovaOperationalChanges.ValidateOrder(edited,operational),"operational writer rejects unowned field "+field);
        }
        var changedLine=(JsonObject)operational.DeepClone();changedLine["Lykes"]!["Lines"]![0]!["Description"]="changed freight";
        Reject(()=>LykesNovaOperationalChanges.ValidateOrder(changedLine,operational),"operational writer rejects individual freight edits even with unchanged totals");
        var notes=(JsonObject)operational.DeepClone();notes["Notes"]="Dispatcher note";notes["FreightOpsState"]="Created";
        LykesNovaOperationalChanges.ValidateOrder(notes,operational);check(true,"operational note and execution metadata do not require stable native IDs");
        check(LykesNovaOperationalChanges.SameNativeTime(JsonValue.Create("2026-11-03T15:02:11.9911831Z"),JsonValue.Create("2026-11-03T10:02:11.9911-05:00")),"Firebird 100-microsecond precision does not create a false schedule conflict");
        check(!LykesNovaOperationalChanges.SameNativeTime(JsonValue.Create("2026-11-03T15:02:11.992Z"),JsonValue.Create("2026-11-03T10:02:11.9911-05:00")),"actual schedule changes remain distinct");
        config.OperationalWritesEnabled=false;
        config.NativeReadOnly=false;
        Reject(()=>LykesNovaMapping.RequireSupportedWrite("order",mvp,config),"operational identity marker prevents native-write mode bypass");
        Reject(()=>LykesNovaMapping.Project(mvp,rows,config),"operational snapshots cannot silently become native identities");
        config.NativeReadOnly=true;config.Enabled=false;
        LykesNovaMapping.RequireSupportedWrite("order",source,config);
        check(true,"disabled MVP profile preserves existing non-Lykes writes");
        config.Enabled=true;config.AllowHeaderTotals=true;config.PackTypes["PC"]="Piece";
        var summarySource=new JsonObject{["Id"]="12345",["TerminalCode"]="N-AUS",["Pieces"]=12,["Weight"]=120,["Cube"]=24};
        var summary=LykesNovaMapping.Project(summarySource,[],config);
        check(summary["Lykes"]!["FreightSource"]!.ToString()=="header-totals"&&summary["Lykes"]!["Lines"]!.AsArray().Count==1&&
            summary["Lykes"]!["Lines"]![0]!["Pieces"]!.GetValue<int>()==12,"header-only order becomes one explicitly labeled line with twelve pieces");
        check(summary["Weight"]!.GetValue<decimal>()==120&&summary["Cube"]!.GetValue<decimal>()==24,"header summary preserves shipment totals without multiplying weight or cube");
        summary["TerminalCode"]="N-AUS";
        check(summary["Lykes"]!["Lines"]![0]!["LineId"]!.ToString()==LykesNovaMapping.Project(summary,[],config)["Lykes"]!["Lines"]![0]!["LineId"]!.ToString(),"repeated header enrollment preserves the same piece identity");
        summary["Pieces"]=13;Reject(()=>LykesNovaMapping.Project(summary,[],config),"changed header piece count does not silently replace tracked pieces");
    }
}
