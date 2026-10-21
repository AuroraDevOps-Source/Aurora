using Dapper;
using Npgsql;

static class MappingChecks
{
    public static async Task Run(string connectionString)
    {
        var settings=new NpgsqlConnectionStringBuilder(connectionString);
        if(settings.Host!="127.0.0.1" || settings.Database!="aurora_test")throw new Exception("Only local restored test DB is allowed.");
        await using var db=new NpgsqlConnection(connectionString);await db.OpenAsync();
        await using var tx=await db.BeginTransactionAsync();
        async Task Exec(string sql,object? args=null)=>await db.ExecuteAsync(sql,args,tx);
        async Task<T> Scalar<T>(string sql,object? args=null)=> (await db.ExecuteScalarAsync<T>(sql,args,tx))!;
        var tenant=await Scalar<Guid>("SELECT tenant_id FROM tenant_product WHERE product_code='aurora' AND is_active LIMIT 1");
        var database=Guid.NewGuid();var user=Guid.NewGuid();var other=Guid.NewGuid();
        var args=new{tenant,database,user,other};
        await Exec("""
            INSERT INTO nova_database(tenant_id,id,name,connection) VALUES(@tenant,@database,@database::text,'{}');
            INSERT INTO "AspNetUsers"("Id","UserName","NormalizedUserName","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
            VALUES(@user,@user::text,@user::text,false,false,false,false,0);
            INSERT INTO user_tenant(tenant_id,user_id,is_default,nova_database_id,nova_user_id) VALUES(@tenant,@user,true,@database,'NOVA_TEST');
            INSERT INTO "AspNetUserRoles"("TenantId","UserId","RoleId") SELECT @tenant,@user,"Id" FROM "AspNetRoles" WHERE "Name"='aurora:User';
            SET LOCAL ROLE aurora_app;
            SELECT set_config('app.user_id','',true),set_config('app.tenant_id','',true);
            """,args);
        void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
        Check(await Scalar<int>("SELECT count(*) FROM nova_database WHERE id=@database",args)==0,"anonymous table lookup remains RLS isolated");
        Check(await Scalar<int>("SELECT count(*) FROM nova_launch_database(@database)",args)==1,"enabled database pre-auth lookup");
        Check(await Scalar<Guid?>("SELECT nova_launch_user(@tenant,@database,'nova_test')",args)==user,"case-normalized explicit mapping");
        Check(await Scalar<Guid?>("SELECT nova_launch_user(@tenant,@other,'NOVA_TEST')",args)==null,"wrong database rejected");
        Check(await Scalar<Guid?>("SELECT nova_launch_user(@other,@database,'NOVA_TEST')",args)==null,"wrong tenant rejected");
        Check(await Scalar<Guid?>("SELECT nova_launch_user(@tenant,@database,'UNKNOWN')",args)==null,"unmapped user rejected");
        await Exec("RESET ROLE; DELETE FROM \"AspNetUserRoles\" WHERE \"UserId\"=@user; SET LOCAL ROLE aurora_app",args);
        Check(await Scalar<Guid?>("SELECT nova_launch_user(@tenant,@database,'NOVA_TEST')",args)==null,"removed access rejected");
        await Exec("RESET ROLE; UPDATE user_tenant SET nova_user_id=NULL WHERE user_id=@user; SET LOCAL ROLE aurora_app",args);
        Check(await Scalar<int>("SELECT count(*) FROM nova_launch_database(@database)",args)==0,"removing mapping disables database launch");
        await tx.RollbackAsync();
        Console.WriteLine("Mapping fixtures rolled back.");
        await using var source=NpgsqlDataSource.Create(connectionString);
        var tenantContext=new TestTenant(tenant);
        var admin=new Aurora.Api.Features.Auth.AdminUsers(null!,null!,new Aurora.Infrastructure.Data.NpgsqlConnectionFactory(source,tenantContext),tenantContext);
        var rows=await admin.List(default);
        Check(rows.Length>0,"administrator user listing deserializes the new mapping column");
    }
    private sealed record TestTenant(Guid Tenant):Aurora.Infrastructure.Tenancy.ITenantContext
    {
        public Guid? TenantIdOrNull=>Tenant;
        public Guid? UserIdOrNull=>null;
    }
}
