using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Aurora.Migrations.Seeding;
using DbUp;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var connectionString = builder.Configuration.GetConnectionString("Aurora")
    ?? throw new InvalidOperationException(
        "Missing ConnectionStrings:Aurora — set it via user-secrets to the aurora_owner connection string.");

// DbUp owns the schema; connects as aurora_owner (BYPASSRLS), which must already exist —
// see the one-time manual bootstrap step (scripts/bootstrap-local-db.sql) run as the Postgres
// superuser before this ever runs for the first time.
var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
if (!result.Successful)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(result.Error);
    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("DbUp scripts applied successfully.");
Console.ResetColor();

if (!builder.Configuration.GetValue<bool>("Seed:DevData"))
{
    return 0;
}

// Same aurora_owner (BYPASSRLS) connection for seeding — deliberately, per doc §3.2: seeding
// tenant-scoped data through the app's normal RLS-subject role is exactly what breaks without it.
builder.Services.AddDbContext<AuroraDbContext>(options => options.UseNpgsql(connectionString));

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AuroraDbContext>();

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuroraDbContext>().ReplaceDefaultEntities<Guid>());

var host = builder.Build();
await DevDataSeeder.SeedAsync(host.Services);

return 0;
