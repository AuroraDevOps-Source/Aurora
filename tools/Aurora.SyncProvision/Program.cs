using System.Text.Json;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

if(args is not ["install-waytest",var settingsPath])throw new ArgumentException("Usage: install-waytest <protected settings path>");
var settings=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(settingsPath))!;
var connection=settings["FirebirdRouting:ConnectionString"].GetString()!;
var parsed=new FbConnectionStringBuilder(connection);
if(!parsed.Database.Equals(@"D:\db\wayl_test\operations.fdb",StringComparison.OrdinalIgnoreCase) || parsed.DataSource!="172.23.0.5")
    throw new InvalidOperationException("This provisioning command is restricted to the authorized WayTest source.");
await using var db=new FbConnection(connection);await db.OpenAsync();
if(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='AURORA_SYNC_CONSUMER'")==0)
{
    var script=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ChangeCapture.sql"));
    var pieces=script.Split("SET TERM ^;");
    var statements=pieces[0].Split(';',StringSplitOptions.RemoveEmptyEntries)
        .Concat(pieces[1].Split("SET TERM ;^")[0].Split('^',StringSplitOptions.RemoveEmptyEntries))
        .Where(s=>!string.IsNullOrWhiteSpace(s));
    await using var tx=db.BeginTransaction();
    foreach(var statement in statements)await db.ExecuteAsync(statement,transaction:tx);
    await tx.CommitAsync();
    Console.WriteLine("WayTest change capture installed transactionally. No consumer enabled or business rows changed.");
}
var triggers=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$TRIGGERS WHERE RDB$TRIGGER_NAME STARTING WITH 'AURORA_SYNC_' AND RDB$TRIGGER_INACTIVE=0");
if(triggers!=7)throw new InvalidOperationException("Change-capture trigger verification failed.");
await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AURORA_SYNC_CONSUMER WHERE BOOTSTRAPPED='Y'");
Console.WriteLine("WayTest change-capture schema verified.");
