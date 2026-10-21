using System.Security.Cryptography;
using System.Text;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Identity;

namespace Aurora.Api.Features.Auth;

public sealed record NovaLaunchRequest(Guid DatabaseId, string Ticket);

public static class NovaLaunch
{
    public static bool ValidTicket(string? ticket) => ticket is { Length: 64 } && ticket.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static string TicketHash(string ticket) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(ticket))).ToLowerInvariant();

    // The DELETE and its returned identity are one operation. Committing before issuing a
    // cookie makes failures fail closed and ensures racing/replayed launches cannot succeed.
    public static async Task<string?> Consume(FbConnection fb, string hash, CancellationToken ct)
    {
        await using var tx = await fb.BeginTransactionAsync(ct);
        var user = await fb.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
            DELETE FROM AURORA_LAUNCH_TICKET
            WHERE TICKET_HASH=@hash AND EXPIRES_AT>CURRENT_TIMESTAMP
              AND CREATED_AT<=CURRENT_TIMESTAMP
              AND EXPIRES_AT<=DATEADD(60 SECOND TO CREATED_AT)
            RETURNING NOVA_USER_ID
            """, new { hash }, tx, commandTimeout: 10, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return user;
    }

    public static void MapNovaLaunch(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/nova-launch", async (NovaLaunchRequest input, HttpContext context,
            NpgsqlConnectionFactory factory, NovaUserConnections connections,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
            ILoggerFactory logs, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (input.DatabaseId == Guid.Empty || !ValidTicket(input.Ticket)) return Results.Unauthorized();
            try
            {
                await using var db = await factory.OpenConnectionAsync(ct);
                var databases = (await db.QueryAsync<LaunchDatabase>(new CommandDefinition(
                    "SELECT * FROM nova_launch_database(@DatabaseId)", new { input.DatabaseId }, commandTimeout: 10, cancellationToken: ct))).ToArray();
                // A database UUID must identify exactly one tenant; ambiguous imports fail closed.
                if (databases.Length != 1) return Results.Unauthorized();
                var database = databases[0];
                var options = connections.Resolve(connections.Read(database.Json), database.TenantId);
                await using var fb = new FbConnection(options.ConnectionString);
                await fb.OpenAsync(ct);
                var novaUser = await Consume(fb, TicketHash(input.Ticket), ct);
                if (string.IsNullOrWhiteSpace(novaUser)) return Results.Unauthorized();
                var userId = await db.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                    "SELECT nova_launch_user(@TenantId,@DatabaseId,@novaUser)",
                    new { database.TenantId, input.DatabaseId, novaUser }, commandTimeout: 10, cancellationToken: ct));
                if (userId is null) return Results.Unauthorized();
                var user = await users.FindByIdAsync(userId.ToString()!);
                if (user is null || !await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user)
                    || await users.GetTwoFactorEnabledAsync(user)) return Results.Unauthorized();
                // Mapping is restricted to the default tenant, matching /connect/authorize.
                await signIn.SignInAsync(user, isPersistent: false, authenticationMethod: "NovaLaunch");
                context.Items[HttpTenantContext.TenantOverrideItemsKey] = database.TenantId;
                context.Items[AppLog.ActorKey] = new AppLog.Actor(user.Id, user.UserName ?? "Nova user");
                context.Items[AppLog.ActionKey] = "Sign in from Nova";
                return Results.NoContent();
            }
            catch (Exception ex) when (ex is FbException or Npgsql.NpgsqlException or TimeoutException or CryptographicException or System.Text.Json.JsonException)
            {
                // Never log tickets, connection strings, parameters or provider error messages.
                logs.CreateLogger("NovaLaunch").LogWarning("Nova launch unavailable ({ErrorType})", ex.GetType().Name);
                return Results.StatusCode(503);
            }
        }).AllowAnonymous().RequireRateLimiting("nova-launch");
    }

    private sealed record LaunchDatabase(Guid TenantId, string Json);
}
