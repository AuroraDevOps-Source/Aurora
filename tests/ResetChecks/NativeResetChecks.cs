using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.Nova;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using Npgsql;

static class NativeResetChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        // Deliberately omit delete triggers, matching the copied Aurora Test database.
        var path=Path.Combine(Path.GetTempPath(),"aurora-reset-"+Guid.NewGuid().ToString("N")+".fdb");
        var cs=new FbConnectionStringBuilder{Database=path,UserID="SYSDBA",Password="test",ServerType=FbServerType.Embedded,ClientLibrary=@"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll",Pooling=false}.ConnectionString;
        FbConnection.CreateDatabase(cs);
        try
        {
            await using var db=new FbConnection(cs);await db.OpenAsync();
            foreach(var ddl in new[]{
                "CREATE TABLE OPS_HEADER(PB_ID BIGINT PRIMARY KEY,PB_TYPE CHAR(1),PB_EXTERNAL_REF VARCHAR(128),PB_DT_CANC TIMESTAMP,PB_LOCATION VARCHAR(12),PB_STATUS BIGINT,PB_STATUS_TIME TIMESTAMP,PB_STATUS_LOC_TYPE CHAR(1),PB_STATUS_LOCATION VARCHAR(12),PBTWEIGH INTEGER,PB_TTL_CUBE INTEGER,PB_CURRENT_DRIVER BIGINT,PB_CURRENT_TRACTOR BIGINT,PB_CURRENT_TRAILER BIGINT,PB_PLANNED_DRIVER BIGINT,PB_PLANNED_TRACTOR BIGINT,PB_PLANNED_TRAILER BIGINT)",
                "CREATE TABLE OPS_STOP_REC(OPS_STOP_ID BIGINT PRIMARY KEY,OPS_ORDER_ID BIGINT,OPS_STOP_NUMBER INTEGER,OPS_TYPE CHAR(1),OPS_MANIFEST_NUMBER BIGINT,OPS_STATUS BIGINT,OPS_STATUS_TIME TIMESTAMP,OPS_STATUS_REASON VARCHAR(80),OPS_UPDATED_BY VARCHAR(8),OPS_LOC_TYPE CHAR(1),OPS_LOCATION VARCHAR(12),OPS_PICKED_UP CHAR(1),OPS_DRIVER1 BIGINT,OPS_DRIVER2 BIGINT,OPS_TRACTOR BIGINT,OPS_TRAILER BIGINT)",
                "CREATE TABLE OPS_MANIFEST_STOPS(OSM_STOP_ID BIGINT,OSM_SEQUENCE INTEGER,OSM_PKUP_DELV_ID BIGINT)",
                "CREATE TABLE DRIVER_PKUP_DELV(DPD_ID BIGINT,DPD_LOAD_NUMBER BIGINT)",
                "CREATE TABLE EQUIP_STATUS(ES_ID BIGINT,ES_MANIFEST_NUMBER BIGINT,ES_STOP_ID BIGINT)",
                "CREATE TABLE CT_STAT(CTS_ID BIGINT,CTS_CODE VARCHAR(12))",
                "CREATE TABLE LOCMAST(LM_ID VARCHAR(12))"})await db.ExecuteAsync(ddl);
            foreach(var field in new[]{"OPS_MANIFESTED_TIME","OPS_ARRIVE_TIME","OPS_DEPART_TIME","OPS_BACKIN_TIME","OPS_RELEASE_TIME","OPS_DISPATCH_TIME","OPS_ONBOARD_TIME","OPS_DELIVERED_TIME","OPS_RECOVERED_TIME","OPS_TO_AGENT_TIME"})await db.ExecuteAsync("ALTER TABLE OPS_STOP_REC ADD "+field+" TIMESTAMP");
            var tenant=new Context(Guid.NewGuid());await using var data=NpgsqlDataSource.Create("Host=localhost;Database=unused;Username=unused");
            var source=new FirebirdRoutingSource(new NpgsqlConnectionFactory(data,tenant),tenant,Options.Create(new FirebirdRoutingOptions{Enabled=true,TenantId=tenant.TenantIdOrNull!.Value,ConnectionString=cs,SourceKey="reset-fixture"}));
            await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(110,'RTR')");
            await db.ExecuteAsync("INSERT INTO LOCMAST VALUES('TPA')");
            await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_EXTERNAL_REF) VALUES(1,'M',@reference)",new{reference=FirebirdRoutingSource.ExportKey(tenant.TenantIdOrNull.Value,Guid.NewGuid(),"TYPE")});
            await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOCATION,PB_STATUS) VALUES(2,'O','TPA',110)");
            await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE) VALUES(3,'M')");
            await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER) VALUES(10,1,1)");
            await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER) VALUES(30,3,1)");
            await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_TYPE,OPS_MANIFEST_NUMBER) VALUES(20,2,2,'D',1)");
            await db.ExecuteAsync("INSERT INTO OPS_MANIFEST_STOPS VALUES(10,1,20)");
            await db.ExecuteAsync("INSERT INTO DRIVER_PKUP_DELV VALUES(5,1)");
            await db.ExecuteAsync("INSERT INTO EQUIP_STATUS VALUES(5,1,10)");
            await source.ResetManifests([1],110,default,rollbackOnly:true);
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS")==1,"reset rollback preserves native membership");
            check(await source.ResetManifests([1],110,default)==1,"triggerless reset returns the order");
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS")==0,"triggerless reset removes membership");
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DRIVER_PKUP_DELV")==0,"triggerless reset removes driver assignments");
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=1")==0,"triggerless reset removes route stops");
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EQUIP_STATUS WHERE ES_MANIFEST_NUMBER IS NOT NULL OR ES_STOP_ID IS NOT NULL")==0,"triggerless reset releases equipment status");
            check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID=3")==1&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_STOP_ID=30")==1,"reset preserves other manifests and stops");
            check(await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OPS_HEADER h JOIN OPS_STOP_REC s ON s.OPS_ORDER_ID=h.PB_ID WHERE {NativeRoutingSql.Ready()}",new{readyStatuses=new[]{110},pickupStatuses=new[]{-1}})==1,"returned order is eligible for native planning");
            check(await source.ResetManifests([1],110,default)==0,"triggerless reset retry is idempotent");
        }
        finally{FbConnection.DropDatabase(cs);}
    }
    private sealed record Context(Guid? TenantIdOrNull):ITenantContext{public Guid? UserIdOrNull=>null;}
}
