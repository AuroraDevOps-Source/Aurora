using System.Text.Json.Nodes;
using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Npgsql;

internal static class PushChecks
{
    public static async Task Run(NpgsqlConnection pg,Guid tenant,Guid connection,string nativeConnection,INovaSyncMapper mapper)
    {
        var rows=(await pg.QueryAsync<Row>("""
            SELECT DISTINCT ON(entity_type) entity_type AS Kind,local_id AS LocalId,external_id AS ExternalId,baseline::text AS Payload
            FROM aurora_sync_mapping WHERE tenant_id=@tenant AND connection_id=@connection
              AND (entity_type<>'order' OR baseline->>'Status'='ReadyToRoute')
              AND (entity_type<>'manifest' OR (baseline->>'Status'='Planning'
                AND EXISTS(SELECT 1 FROM aurora_terminal t WHERE t.tenant_id=@tenant AND t.code=baseline#>>'{Details,OriginTerminal}')
                AND EXISTS(SELECT 1 FROM aurora_terminal t WHERE t.tenant_id=@tenant AND t.code=baseline#>>'{Details,DestinationTerminal}')))
            ORDER BY entity_type,external_id DESC
            """,new{tenant,connection})).ToArray();
        if(rows.Length!=6)throw new Exception("Rollback checks require all six entity types.");
        await using var db=new FbConnection(nativeConnection);await db.OpenAsync();
        var failures=0;
        foreach(var row in rows)
        {
            await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait,WaitTimeout=TimeSpan.FromSeconds(5)});
            try
            {
                var payload=JsonNode.Parse(row.Payload)!.AsObject();
                var change=new SyncChange(Guid.NewGuid(),connection,row.Kind,row.LocalId,999,"upsert",payload.ToJsonString(),1,Guid.NewGuid());
                var receipt=await mapper.Push(change,db,tx,default);
                if(receipt.ExternalId!=row.ExternalId)throw new Exception("Updating an existing record changed its identity.");
                var after=(await mapper.Read(new(1,row.Kind,row.ExternalId,"U"),db,tx,default)).Single(s=>s.EntityType==row.Kind&&s.ExternalId==row.ExternalId);
                if(!JsonNode.DeepEquals(payload,after.Data))
                {
                    var fields=payload.Select(p=>p.Key).Union(after.Data.Select(p=>p.Key)).Where(k=>!JsonNode.DeepEquals(payload[k],after.Data[k]));
                    throw new Exception("No-op round trip changed: "+string.Join(", ",fields));
                }
                switch(row.Kind)
                {
                    case "terminal":payload["Name"]="Aurora rollback check";break;
                    case "customer":case "driver":payload["Phone"]="5550000000";break;
                    case "equipment":payload["Unit"]!["Plate"]="SYNCCHK";break;
                    case "order":payload["Reference"]="SYNC-CHECK";break;
                    case "manifest":payload["Details"]!["Notes"]="Aurora rollback check";break;
                }
                await mapper.Push(change with{Id=Guid.NewGuid(),Payload=payload.ToJsonString()},db,tx,default);
                after=(await mapper.Read(new(2,row.Kind,row.ExternalId,"U"),db,tx,default)).Single(s=>s.EntityType==row.Kind&&s.ExternalId==row.ExternalId);
                if(!JsonNode.DeepEquals(payload,after.Data))throw new Exception("Edited field did not round trip to Nova.");
                Console.WriteLine("PASS rollback no-op, edited write + readback "+row.Kind);
            }
            catch(Exception ex){failures++;Console.WriteLine($"FAIL rollback {row.Kind}: {ex.GetType().Name}: {ex.Message}");}
            finally{await tx.RollbackAsync();}
        }
        foreach(var row in rows)
        {
            await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait,WaitTimeout=TimeSpan.FromSeconds(5)});
            try
            {
                var payload=JsonNode.Parse(row.Payload)!.AsObject();
                var key="SC"+Guid.NewGuid().ToString("N")[..8];
                switch(row.Kind)
                {
                    case "terminal":case "customer":payload["Code"]=key;break;
                    case "driver":payload["Number"]=key;break;
                    case "equipment":payload["Unit"]!["Id"]=key;break;
                    case "order":payload["Id"]="9"+Random.Shared.Next(10000000,99999999);payload["Status"]="ReadyToRoute";break;
                    case "manifest":payload["Number"]=key;payload["Status"]="Planning";payload["Orders"]=new JsonArray();payload["Vehicle"]="";payload["Details"]!["DriverId"]=null;break;
                }
                var receipt=await mapper.Push(new(Guid.NewGuid(),connection,row.Kind,"new-"+key,1,"upsert",payload.ToJsonString(),1,Guid.NewGuid()),db,tx,default);
                var after=await mapper.Read(new(1,row.Kind,receipt.ExternalId,"U"),db,tx,default);
                if(!after.Any(s=>s.EntityType==row.Kind&&!s.Deleted))throw new Exception("Created record cannot be read back.");
                Console.WriteLine("PASS rollback create + readback "+row.Kind);
            }
            catch(Exception ex){failures++;Console.WriteLine($"FAIL rollback create {row.Kind}: {ex.GetType().Name}: {ex.Message}");}
            finally{await tx.RollbackAsync();}
        }
        Console.WriteLine($"Rollback writes: {2*rows.Length-failures} passed, {failures} failed. No native business changes committed.");
        if(failures>0)Environment.ExitCode=1;
    }
    private sealed record Row(string Kind,string LocalId,string ExternalId,string Payload);
}
