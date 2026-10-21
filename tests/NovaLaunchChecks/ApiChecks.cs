using System.Net;
using System.Net.Http.Json;
using Aurora.Api.Features.Auth;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Threading.RateLimiting;

static class ApiChecks
{
    public static async Task Run(string ownerConnection,string appConnection)
    {
        var settings=new NpgsqlConnectionStringBuilder(ownerConnection);
        if(settings.Host!="127.0.0.1" || settings.Database!="aurora_test")throw new Exception("Local restored DB only");
        await using var db=new NpgsqlConnection(ownerConnection);await db.OpenAsync();
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:18763");
        builder.Configuration["Hosting:PublicOrigin"]="https://aurora.test";
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ITenantContext,HttpTenantContext>();
        builder.Services.AddScoped<NpgsqlConnectionFactory>();
        builder.Services.AddScoped<TenantConnectionInterceptor>();
        builder.Services.AddSingleton(NpgsqlDataSource.Create(appConnection));
        builder.Services.AddDbContext<AuroraDbContext>((sp,o)=>{o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>());o.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());});
        builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<AuroraDbContext>().AddSignInManager().AddDefaultTokenProviders();
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme);
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o=>o.AddPolicy("nova-launch",_=>RateLimitPartition.GetNoLimiter("checks")));
        await using var app=builder.Build();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapNovaApiLaunch();
        var uid=Guid.NewGuid();var database=Guid.NewGuid();
        var tenant=await db.ExecuteScalarAsync<Guid>("SELECT tenant_id FROM tenant_product WHERE product_code='aurora' AND is_active LIMIT 1");
        var name="nova-api-check-"+uid.ToString("N");var password="Synthetic!"+Guid.NewGuid().ToString("N");
        var identity=new ApplicationUser{Id=uid,UserName=name,NormalizedUserName=name.ToUpperInvariant(),SecurityStamp=Guid.NewGuid().ToString("N"),LockoutEnabled=true};
        var hash=new PasswordHasher<ApplicationUser>().HashPassword(identity,password);
        var args=new{uid,database,tenant,name,hash,stamp=identity.SecurityStamp};
        await db.ExecuteAsync("""
            INSERT INTO "AspNetUsers"("Id","UserName","NormalizedUserName","PasswordHash","SecurityStamp","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
            VALUES(@uid,@name,upper(@name),@hash,@stamp,false,false,false,true,0);
            INSERT INTO nova_database(tenant_id,id,name,connection) VALUES(@tenant,@database,@name,'{}');
            INSERT INTO user_tenant(tenant_id,user_id,is_default,nova_database_id,nova_api_login) VALUES(@tenant,@uid,true,@database,true);
            INSERT INTO "AspNetUserRoles"("TenantId","UserId","RoleId") SELECT @tenant,@uid,"Id" FROM "AspNetRoles" WHERE "Name"='aurora:User';
            """,args);
        try
        {
            await app.StartAsync();
            using var client=new HttpClient(new HttpClientHandler{UseCookies=false}){BaseAddress=new Uri("http://127.0.0.1:18763")};
            int count=0;
            void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
            async Task<HttpResponseMessage> Login(string? secret=null)=>await client.PostAsJsonAsync("/api/auth/nova/token",new{userName=name,password=secret??password});
            async Task<string> Issue(){using var response=await Login();Check(response.StatusCode==HttpStatusCode.OK,"enabled account issues launch URL");Check(!response.Headers.Contains("Set-Cookie"),"issuance does not sign desktop client in");var result=(await response.Content.ReadFromJsonAsync<NovaLaunchUrl>())!;Check(result.ExpiresIn==60 && result.LaunchUrl.StartsWith("https://aurora.test/nova-launch#code="),"fixed trusted URL and 60-second contract");return new Uri(result.LaunchUrl).Fragment[6..];}
            async Task<HttpResponseMessage> Redeem(string code)=>await client.PostAsJsonAsync("/api/auth/nova/redeem",new{code});
            using(var invalid=await Login("wrong"))Check(invalid.StatusCode==HttpStatusCode.Unauthorized,"wrong credentials rejected");
            Check(await db.ExecuteScalarAsync<int>("SELECT \"AccessFailedCount\" FROM \"AspNetUsers\" WHERE \"Id\"=@uid",args)==1,"failed password contributes to account lockout");
            var code=await Issue();
            using(var good=await Redeem(code))Check(good.StatusCode==HttpStatusCode.NoContent && good.Headers.Contains("Set-Cookie") && good.Headers.CacheControl?.NoStore==true,"redemption creates browser cookie with no-store");
            using(var replay=await Redeem(code))Check(replay.StatusCode==HttpStatusCode.Unauthorized && !replay.Headers.Contains("Set-Cookie"),"replay cannot create a session");
            code=await Issue();
            await db.ExecuteAsync("UPDATE nova_api_code SET expires_at=clock_timestamp()-interval '1 second' WHERE user_id=@uid",args);
            using(var expired=await Redeem(code))Check(expired.StatusCode==HttpStatusCode.Unauthorized,"expired code rejected");
            code=await Issue();
            var racing=await Task.WhenAll(Redeem(code),Redeem(code));
            Check(racing.Count(r=>r.StatusCode==HttpStatusCode.NoContent)==1 && racing.Count(r=>r.StatusCode==HttpStatusCode.Unauthorized)==1,"simultaneous redemption has exactly one winner");foreach(var response in racing)response.Dispose();
            code=await Issue();
            await db.ExecuteAsync("UPDATE user_tenant SET nova_api_login=false WHERE user_id=@uid",args);
            using(var revoked=await Redeem(code))Check(revoked.StatusCode==HttpStatusCode.Unauthorized,"disabling integration revokes pending code");
            using(var disabled=await Login())Check(disabled.StatusCode==HttpStatusCode.Unauthorized,"ordinary account cannot mint launch codes");
            await db.ExecuteAsync("UPDATE user_tenant SET nova_api_login=true WHERE user_id=@uid",args);
            code=await Issue();
            await db.ExecuteAsync("UPDATE \"AspNetUsers\" SET \"SecurityStamp\"='changed' WHERE \"Id\"=@uid",args);
            using(var changed=await Redeem(code))Check(changed.StatusCode==HttpStatusCode.Unauthorized,"password/security stamp change revokes pending code");
            code=await Issue();
            await db.ExecuteAsync("UPDATE user_tenant SET nova_database_id=NULL,nova_api_login=false WHERE user_id=@uid",args);
            using(var changed=await Redeem(code))Check(changed.StatusCode==HttpStatusCode.Unauthorized,"database access removal revokes code");
            await db.ExecuteAsync("UPDATE user_tenant SET nova_database_id=@database,nova_api_login=true WHERE user_id=@uid; UPDATE \"AspNetUsers\" SET \"TwoFactorEnabled\"=true WHERE \"Id\"=@uid",args);
            using(var mfa=await Login())Check(mfa.StatusCode==HttpStatusCode.Unauthorized,"MFA accounts cannot bypass second factor");
            await db.ExecuteAsync("UPDATE \"AspNetUsers\" SET \"TwoFactorEnabled\"=false,\"LockoutEnd\"=clock_timestamp()+interval '1 hour' WHERE \"Id\"=@uid",args);
            using(var locked=await Login())Check(locked.StatusCode==HttpStatusCode.Unauthorized,"locked-out account rejected");
            Console.WriteLine($"{count} Nova API checks passed.");
        }
        finally
        {
            await app.StopAsync();
            await db.ExecuteAsync("DELETE FROM \"AspNetUsers\" WHERE \"Id\"=@uid; DELETE FROM nova_database WHERE id=@database AND tenant_id=@tenant",args);
            Console.WriteLine("Synthetic API user, database, codes and memberships removed.");
        }
    }
}
