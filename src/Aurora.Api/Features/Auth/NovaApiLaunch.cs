using System.Security.Cryptography;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Microsoft.AspNetCore.Identity;

namespace Aurora.Api.Features.Auth;

public sealed record NovaCodeRequest(string Code);
public sealed record NovaLaunchUrl(string LaunchUrl, int ExpiresIn);

public static class NovaApiLaunch
{
    public static void MapNovaApiLaunch(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/nova/token", Issue).AllowAnonymous().RequireRateLimiting("nova-launch");
        app.MapPost("/api/auth/nova/redeem", Redeem).AllowAnonymous().RequireRateLimiting("nova-launch");
    }

    static void PrivateResponse(HttpContext context)
    {
        context.Response.Headers.CacheControl="no-store";
        context.Response.Headers.Pragma="no-cache";
        context.Response.Headers["Referrer-Policy"]="no-referrer";
    }

    public static async Task<IResult> Issue(LoginRequest input, HttpContext context,
        UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        NpgsqlConnectionFactory factory, IConfiguration config, CancellationToken ct)
    {
        PrivateResponse(context);
        if (string.IsNullOrWhiteSpace(input.UserName) || input.UserName.Length>256 ||
            string.IsNullOrEmpty(input.Password) || input.Password.Length>1024) return Results.Unauthorized();
        // Use only the configured origin, never a caller-controlled Host or redirect URL.
        if (!Uri.TryCreate(config["Hosting:PublicOrigin"],UriKind.Absolute,out var origin) ||
            origin.Scheme!="https" || origin.AbsolutePath!="/" || origin.UserInfo!="" || origin.Query!="" || origin.Fragment!="")
            return Results.StatusCode(503);
        var user=await users.FindByNameAsync(input.UserName.Trim());
        if(user is null) return Results.Unauthorized();
        await using var db=await factory.OpenConnectionAsync(ct);
        var account=await db.QuerySingleOrDefaultAsync<Account>(new CommandDefinition(
            "SELECT * FROM nova_api_account(@Id)",new{user.Id},cancellationToken:ct));
        if(account is null) return Results.Unauthorized();
        var check=await signIn.CheckPasswordSignInAsync(user,input.Password,lockoutOnFailure:true);
        if(!check.Succeeded || await users.GetTwoFactorEnabledAsync(user)) return Results.Unauthorized();
        var code=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        account=await db.QuerySingleOrDefaultAsync<Account>(new CommandDefinition(
            "SELECT * FROM nova_api_issue(@Id,@hash,@stamp)",
            new{user.Id,hash=NovaLaunch.TicketHash(code),stamp=await users.GetSecurityStampAsync(user)},cancellationToken:ct));
        if(account is null) return Results.Unauthorized();
        context.Items[HttpTenantContext.TenantOverrideItemsKey]=account.TenantId;
        context.Items[AppLog.ActorKey]=new AppLog.Actor(user.Id,user.UserName??"Nova integration");
        // No browser cookie or reusable access token is issued to the desktop application.
        return Results.Ok(new NovaLaunchUrl(new Uri(origin,"/nova-launch#code="+code).AbsoluteUri,60));
    }

    public static async Task<IResult> Redeem(NovaCodeRequest input, HttpContext context,
        UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        NpgsqlConnectionFactory factory, CancellationToken ct)
    {
        PrivateResponse(context);
        if(!NovaLaunch.ValidTicket(input.Code)) return Results.Unauthorized();
        await using var db=await factory.OpenConnectionAsync(ct);
        // Autocommit DELETE RETURNING consumes the code before any cookie is issued.
        var code=await db.QuerySingleOrDefaultAsync<ConsumedCode>(new CommandDefinition(
            "SELECT * FROM nova_api_consume(@hash)",new{hash=NovaLaunch.TicketHash(input.Code)},cancellationToken:ct));
        if(code is null) return Results.Unauthorized();
        var user=await users.FindByIdAsync(code.UserId.ToString());
        if(user is null || !await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user) ||
            await users.GetTwoFactorEnabledAsync(user) || await users.GetSecurityStampAsync(user)!=code.SecurityStamp)
            return Results.Unauthorized();
        var account=await db.QuerySingleOrDefaultAsync<Account>(new CommandDefinition(
            "SELECT * FROM nova_api_account(@Id)",new{user.Id},cancellationToken:ct));
        if(account is null || account.TenantId!=code.TenantId || account.DatabaseId!=code.DatabaseId)
            return Results.Unauthorized();
        await signIn.SignInAsync(user,isPersistent:false,authenticationMethod:"NovaApi");
        context.Items[HttpTenantContext.TenantOverrideItemsKey]=account.TenantId;
        context.Items[AppLog.ActorKey]=new AppLog.Actor(user.Id,user.UserName??"Nova integration");
        return Results.NoContent();
    }

    private sealed record Account(Guid TenantId,Guid DatabaseId);
    private sealed record ConsumedCode(Guid UserId,Guid TenantId,Guid DatabaseId,string SecurityStamp);
}
