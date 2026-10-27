using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova.Sync;

public static class NovaSyncBootstrap
{
    public static async Task ReclassifyOrders(string connectionString,Guid consumer,CancellationToken ct)
    {
        await using var db=new FbConnection(connectionString);await db.OpenAsync(ct);
        await using var tx=db.BeginTransaction();
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AURORA_SYNC_EVENT(EVENT_ID,CONSUMER_ID,ENTITY_TYPE,ENTITY_ID,CHANGE_KIND)
            SELECT GEN_ID(AURORA_SYNC_EVENT_GEN,1),@key,'order-or-manifest',CAST(h.PB_ID AS VARCHAR(80)),'U'
            FROM OPS_HEADER h WHERE h.PB_TYPE='O' AND h.PB_DT_CANC IS NULL
              AND NOT EXISTS(SELECT 1 FROM AURORA_SYNC_EVENT e WHERE e.CONSUMER_ID=@key
                AND e.ENTITY_TYPE='order-or-manifest' AND e.ENTITY_ID=CAST(h.PB_ID AS VARCHAR(80)))
            """,new{key=consumer.ToString()},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }
    public static async Task<int> Pending(string connectionString,Guid consumer,CancellationToken ct)
    {
        await using var db=new FbConnection(connectionString);await db.OpenAsync(ct);
        return await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@key",new{key=consumer.ToString()},cancellationToken:ct));
    }
    public static async Task EnsureSeeded(string connectionString,Guid consumer,CancellationToken ct)
    {
        await using var db=new FbConnection(connectionString);await db.OpenAsync(ct);
        // Schema provisioning is a deployment step, not a background permission escalation.
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='AURORA_SYNC_CONSUMER'",cancellationToken:ct))!=1)
            throw new InvalidOperationException("Install Nova change capture before enabling synchronization.");
        var key=consumer.ToString();
        // Enable capture before enumerating keys. Concurrent inserts/updates are then
        // queued even if their key wasn't visible to the initial enumeration.
        await using(var tx=db.BeginTransaction())
        {
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM AURORA_SYNC_CONSUMER WHERE CONSUMER_ID=@key",new{key},tx,cancellationToken:ct))==0)
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO AURORA_SYNC_CONSUMER(CONSUMER_ID,ENABLED) VALUES(@key,'Y')",new{key},tx,cancellationToken:ct));
            await tx.CommitAsync(ct);
        }
        await using(var tx=db.BeginTransaction())
        {
            var seeded=await db.QuerySingleAsync<string>(new CommandDefinition("SELECT BOOTSTRAPPED FROM AURORA_SYNC_CONSUMER WHERE CONSUMER_ID=@key WITH LOCK",new{key},tx,cancellationToken:ct));
            if(seeded=="Y"){await tx.CommitAsync(ct);return;}
            foreach(var item in new[]{("terminal","LOCMAST","LM_ID","1=1"),("customer","ACCOUNT","AC_CUST","1=1"),("equipment","EQUIP_HEADER","EH_ID","EH_DELETE_DATE IS NULL"),("order-or-manifest","OPS_HEADER","PB_ID","PB_TYPE='O' AND PB_DT_CANC IS NULL"),("order-or-manifest","OPS_HEADER","PB_ID","PB_TYPE='M' AND PB_DT_CANC IS NULL")})
                await db.ExecuteAsync(new CommandDefinition($"INSERT INTO AURORA_SYNC_EVENT(EVENT_ID,CONSUMER_ID,ENTITY_TYPE,ENTITY_ID,CHANGE_KIND) SELECT GEN_ID(AURORA_SYNC_EVENT_GEN,1),@key,@kind,CAST({item.Item3} AS VARCHAR(80)),'U' FROM {item.Item2} WHERE {item.Item4}",new{key,kind=item.Item1},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("UPDATE AURORA_SYNC_CONSUMER SET BOOTSTRAPPED='Y' WHERE CONSUMER_ID=@key",new{key},tx,cancellationToken:ct));
            await tx.CommitAsync(ct);
        }
    }
}
