using Dapper;
using FirebirdSql.Data.FirebirdClient;

internal static class StopEtaChecks
{
    internal static async Task Run(FbConnection db, Action<bool,string> check)
    {
        await db.ExecuteAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"StopEta.sql")));
        check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ETA_TIME IS NOT NULL")==0,
            "ETA migration leaves existing stops unset");
        var expected=new DateTime(2026,11,3,1,35,42);
        await using var tx=db.BeginTransaction();
        await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_ETA_TIME=@expected WHERE OPS_STOP_ID=200",new{expected},tx);
        check(await db.ExecuteScalarAsync<DateTime>("SELECT OPS_ETA_TIME FROM OPS_STOP_REC WHERE OPS_STOP_ID=200",transaction:tx)==expected,
            "ETA retains next-day date and time independently of appointments");
        await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_ETA_TIME=NULL WHERE OPS_STOP_ID=200",transaction:tx);
        check(await db.ExecuteScalarAsync<DateTime?>("SELECT OPS_ETA_TIME FROM OPS_STOP_REC WHERE OPS_STOP_ID=200",transaction:tx)==null,
            "ETA can be cleared without a synthetic date");
        await tx.RollbackAsync();
    }
}
