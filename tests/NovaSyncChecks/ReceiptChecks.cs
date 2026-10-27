using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Text.Json.Nodes;

static class ReceiptChecks
{
    public static async Task Run(string cs,Guid consumer,Action<bool,string> check)
    {
        await using var db=new FbConnection(cs);await db.OpenAsync();
        await db.ExecuteAsync("DELETE FROM AURORA_SYNC_EVENT");
        var mapper=new Mapper();
        var provider=new NovaSyncProvider(consumer,cs,mapper);
        var change=new SyncChange(Guid.NewGuid(),consumer,"order","100",0,"upsert","{}",1,Guid.NewGuid());
        var receipt=await provider.Push(change,default);
        var replay=await provider.Push(change with{Attempts=2,LeaseId=Guid.NewGuid()},default);
        check(receipt==replay&&mapper.Calls==1,"native receipt prevents duplicate write after lost response");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID=100")==1,"native business write committed with receipt");
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@consumer",new{consumer=consumer.ToString()})==0,"sync push suppresses its own capture events");
        try{await provider.Push(change with{Payload="{\"different\":true}"},default);throw new Exception("Mismatched receipt accepted");}
        catch(SyncConflictException){check(true,"native receipt rejects changed payload under same event ID");}
        mapper.Fail=true;
        try{await provider.Push(change with{Id=Guid.NewGuid(),LocalId="101"},default);throw new Exception("Failing mapping accepted");}
        catch(FormatException){check(true,"native mapping failure rolls back");}
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID=101")==0&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_RECEIPT")==1,"failed native transaction leaves neither business row nor receipt");
        await db.ExecuteAsync("UPDATE OPS_HEADER SET TEST_VALUE='external' WHERE PB_ID=100");
        var batch=await provider.Pull(null,default);
        check(batch.Changes.Count==1&&batch.Changes[0].Data["value"]!.ToString()=="external","pull returns a committed native snapshot");
        var again=await provider.Pull(batch.Checkpoint,default);
        check(again.Changes.Single().EventId==batch.Changes.Single().EventId,"unacknowledged delivery repeats with stable inbox identity");
        await db.ExecuteAsync("UPDATE OPS_HEADER SET TEST_VALUE='newer' WHERE PB_ID=100");
        await provider.Acknowledge(batch,default);
        var newer=await provider.Pull(null,default);
        check(newer.Changes.Count==1&&newer.Changes[0].Data["value"]!.ToString()=="newer","acknowledging a batch retains later native changes");
        check(newer.Changes[0].EventId!=batch.Changes[0].EventId,"new external revision has a distinct inbox identity");
    }
    sealed class Mapper:INovaSyncMapper
    {
        public int Calls;public bool Fail;
        public async Task<ExternalReceipt> Push(SyncChange change,FbConnection db,FbTransaction tx,CancellationToken ct)
        {
            Calls++;
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO OPS_HEADER(PB_ID,TEST_VALUE) VALUES(@id,'local')",new{id=long.Parse(change.LocalId)},tx,cancellationToken:ct));
            if(Fail)throw new FormatException("Fixture mapping failed.");
            return new(change.LocalId,"r1");
        }
        public async Task<IReadOnlyList<NovaSyncSnapshot>> Read(NovaChangeEvent change,FbConnection db,FbTransaction tx,CancellationToken ct)
        {
            var value=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT TEST_VALUE FROM OPS_HEADER WHERE PB_ID=@id",new{id=long.Parse(change.EntityId)},tx,cancellationToken:ct));
            return [new("order",change.EntityId,value??"deleted",new JsonObject{["value"]=value},value is null)];
        }
    }
}
