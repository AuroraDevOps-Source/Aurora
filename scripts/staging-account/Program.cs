using System.Text.Json;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

// One-off staging maintenance, never hosted by the API or included in its publish.
// The user explicitly requested a six-digit demo password for this exact account.
// Identity's normal API password policy is deliberately NOT modified.
var targetId = Guid.Parse("01a08138-7cb9-7a10-8bd8-450a7bfd755e");
var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Aurora")
    ?? throw new InvalidOperationException("Missing staging connection.");
var parsed = new NpgsqlConnectionStringBuilder(connection);
if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") != "Staging" ||
    Environment.GetEnvironmentVariable("Hosting__PublicOrigin") != "https://user.aurorasoftware.com" ||
    parsed.Host != "db" || parsed.Database != "aurora" || parsed.Username != "aurora_owner")
    throw new InvalidOperationException("This utility is restricted to the known Aurora test installation.");

var services = new ServiceCollection();
services.AddLogging(); // No console provider: do not emit SQL, hashes or credentials.
services.AddDbContext<AuroraDbContext>(options => options.UseNpgsql(connection));
services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
}).AddRoles<ApplicationRole>().AddEntityFrameworkStores<AuroraDbContext>();
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
var user = await manager.FindByIdAsync(targetId.ToString())
    ?? throw new InvalidOperationException("Expected linked test account was not found.");
if (user.Email != "admin@aurora.local" ||
    !await db.UserTenants.AnyAsync(t => t.UserId == targetId && t.TenantId == tenantId))
    throw new InvalidOperationException("Test account identity or tenant did not match.");
var roles = await db.Set<ApplicationUserRole>().Where(r => r.UserId == targetId)
    .Select(r => new { r.TenantId, r.RoleId }).ToListAsync();
var memberships = await db.UserTenants.Where(t => t.UserId == targetId)
    .Select(t => new { t.TenantId, t.IsDefault, t.JoinedUtc }).ToListAsync();
Console.WriteLine($"Verified test account {user.Id}, username {user.UserName}; {memberships.Count} memberships and {roles.Count} role links.");
if (args.SequenceEqual(new[] { "--inspect" })) return;
if (!args.SequenceEqual(new[] { "--apply-approved-demo-login" }))
    throw new InvalidOperationException("Explicit demo maintenance mode required.");
if (user.UserName != "admin@aurora.local")
    throw new InvalidOperationException("The username changed since this operation was prepared; refusing to overwrite it.");
var input = JsonSerializer.Deserialize<DemoLogin>(await Console.In.ReadToEndAsync())
    ?? throw new InvalidOperationException("Missing login input.");
if (input.UserName != "bbatts" || input.Password.Length != 6 || !input.Password.All(char.IsAsciiDigit))
    throw new InvalidOperationException("Expected the explicitly approved test username and six-digit demo password.");
var collision = await manager.FindByNameAsync(input.UserName);
if (collision is not null && collision.Id != targetId)
    throw new InvalidOperationException("The requested username belongs to another account.");

var backupPath = $"/task-private/account-pre-demo-login-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
await using (var backup = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write))
{
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(backupPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    await JsonSerializer.SerializeAsync(backup, new { User = user, Roles = roles, Memberships = memberships });
}
Require(await manager.SetUserNameAsync(user, input.UserName));
Require(await manager.RemovePasswordAsync(user));
Require(await manager.AddPasswordAsync(user, input.Password));
Require(await manager.ResetAccessFailedCountAsync(user));
Require(await manager.SetLockoutEndDateAsync(user, null));
if (!await manager.CheckPasswordAsync(user, input.Password))
    throw new InvalidOperationException("New password verification failed.");
var afterRoles = await db.Set<ApplicationUserRole>().Where(r => r.UserId == targetId)
    .Select(r => new { r.TenantId, r.RoleId }).ToListAsync();
var afterMemberships = await db.UserTenants.Where(t => t.UserId == targetId)
    .Select(t => new { t.TenantId, t.IsDefault, t.JoinedUtc }).ToListAsync();
if (!roles.ToHashSet().SetEquals(afterRoles) || !memberships.ToHashSet().SetEquals(afterMemberships))
    throw new InvalidOperationException("Account permissions changed unexpectedly; rolling back.");
await transaction.CommitAsync();
Console.WriteLine($"Updated {input.UserName}; existing subject, email, memberships and roles preserved. Protected account backup: {backupPath}");

static void Require(IdentityResult result)
{
    if (!result.Succeeded)
        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Code)));
}
sealed record DemoLogin(string UserName, string Password);
