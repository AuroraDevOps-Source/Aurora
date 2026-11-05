using Aurora.Integrations.Nova;
using Dapper;
using Npgsql;

internal static class SnapshotChecks
{
    internal static async Task<int> Run(string connection)
    {
        var settings=new NpgsqlConnectionStringBuilder(connection);
        if(settings.Host!="127.0.0.1" || settings.Username!="terminal_check")throw new Exception("Use a disposable local terminal_check cluster.");
        await using var db=new NpgsqlConnection(connection);await db.OpenAsync();
        await using var tx=await db.BeginTransactionAsync();
        await db.ExecuteAsync("""
            CREATE TEMP TABLE aurora_order_source(tenant_id uuid,id uuid,request jsonb);
            CREATE TEMP TABLE aurora_order(tenant_id uuid,source_id uuid,id text,scheduled_at timestamptz,customer text,city text,status text,terminal_id uuid,details jsonb,deleted_at timestamptz,revision int DEFAULT 0,PRIMARY KEY(tenant_id,source_id,id));
            CREATE TEMP TABLE aurora_planning_draft(tenant_id uuid,source_id uuid,order_ids text[],finished_session uuid,cancelled_at timestamptz);
            CREATE TEMP TABLE aurora_manifest_order(tenant_id uuid,source_id uuid,order_id text);
            """,transaction:tx);
        var tenant=Guid.NewGuid();var a=Guid.NewGuid();var b=Guid.NewGuid();
        var checks=0;
        async Task Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;await Task.CompletedTask;}
        async Task Upsert(Guid source)=>_ = await db.ExecuteAsync(NativeSnapshotSql.Upsert,new{TenantId=tenant,source,Id="FB-10",time=DateTimeOffset.UtcNow,Customer="Received freight",City="B",json="{}"},tx);
        await db.ExecuteAsync("""INSERT INTO aurora_order_source VALUES(@tenant,@a,'{"reporting":{"firebirdSource":"check"}}'),(@tenant,@b,'{"reporting":{"firebirdSource":"check"}}')""",new{tenant,a,b},tx);
        await Upsert(a);await Upsert(b);
        await db.ExecuteAsync(NativeSnapshotSql.Withdraw,new{TenantId=tenant,SourceKey="check",keys=new[]{"FB-10@"+b}},tx);
        await Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_order WHERE deleted_at IS NULL AND source_id=@b",new{b},tx)==1,"Received order remains available at B");
        await Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_order WHERE deleted_at IS NULL AND source_id=@a",new{a},tx)==0,"Old-terminal snapshot withdrawn even when native order ID is unchanged");
        await db.ExecuteAsync("UPDATE aurora_order SET status='Routed' WHERE source_id=@b; INSERT INTO aurora_manifest_order VALUES(@tenant,@b,'FB-10')",new{tenant,b},tx);
        await Upsert(b);
        await db.ExecuteAsync(NativeSnapshotSql.ReleaseAssignment,new{TenantId=tenant,source=b,id="FB-10"},tx);
        await Check(await db.ExecuteScalarAsync<string>("SELECT status FROM aurora_order WHERE source_id=@b",new{b},tx)=="ReadyToRoute","Returning freight resets old routed snapshot");
        await Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_manifest_order",transaction:tx)==0,"Obsolete local active-assignment index released");
        await db.ExecuteAsync("UPDATE aurora_order SET status='Routed' WHERE source_id=@b; INSERT INTO aurora_manifest_order VALUES(@tenant,@b,'FB-10'); INSERT INTO aurora_planning_draft VALUES(@tenant,@b,ARRAY['FB-10'],NULL,NULL)",new{tenant,b},tx);
        await Upsert(b);
        await db.ExecuteAsync(NativeSnapshotSql.ReleaseAssignment,new{TenantId=tenant,source=b,id="FB-10"},tx);
        await db.ExecuteAsync(NativeSnapshotSql.Withdraw,new{TenantId=tenant,SourceKey="check",keys=Array.Empty<string>()},tx);
        await Check(await db.ExecuteScalarAsync<string>("SELECT status FROM aurora_order WHERE source_id=@b AND deleted_at IS NULL",new{b},tx)=="Routed","Open plan protects original snapshot during refresh");
        await Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_manifest_order",transaction:tx)==1,"Open plan protects assignment index");
        await tx.RollbackAsync();return checks;
    }
}
