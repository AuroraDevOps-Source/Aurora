using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova.Sync;

public sealed record NovaChangeEvent(long EventId,string EntityType,string EntityId,string ChangeKind);

// Transport only. Mapping business fields to Aurora belongs to the Nova adapter,
// not to the queue or to Aurora's routing domain.
public static class NovaChangeQueue
{
    public static async Task<NovaChangeEvent[]> Read(FbConnection db,FbTransaction tx,Guid consumerId,CancellationToken ct)
        => (await db.QueryAsync<NovaChangeEvent>(new CommandDefinition("""
            SELECT FIRST 200 EVENT_ID AS EventId,TRIM(ENTITY_TYPE) AS EntityType,TRIM(ENTITY_ID) AS EntityId,CHANGE_KIND AS ChangeKind
            FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@consumer ORDER BY EVENT_ID
            """,new{consumer=consumerId.ToString()},tx,cancellationToken:ct))).ToArray();

    // Call only AFTER Aurora's import/inbox transaction commits. If acknowledgement
    // fails, rereading is safe because the inbox rejects already-applied event IDs.
    public static Task Acknowledge(FbConnection db,FbTransaction tx,Guid consumerId,long[] ids,CancellationToken ct)
    {
        if(ids.Length==0)return Task.CompletedTask;
        return db.ExecuteAsync(new CommandDefinition("DELETE FROM AURORA_SYNC_EVENT WHERE CONSUMER_ID=@consumer AND EVENT_ID IN @ids",
            new{consumer=consumerId.ToString(),ids},tx,cancellationToken:ct));
    }

    public static Task SetOrigin(FbConnection db,FbTransaction tx,Guid consumerId,CancellationToken ct)
        => db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT RDB$SET_CONTEXT('USER_TRANSACTION','AURORA_SYNC_ORIGIN',@consumer) FROM RDB$DATABASE",
            new{consumer=consumerId.ToString()},tx,cancellationToken:ct));
}
