using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Aurora.Migrations.Seeding;

/// <summary>
/// Dev-only seeding that goes through real manager APIs (UserManager, IOpenIddictApplicationManager)
/// rather than raw SQL, because both the password hash and the application's permission/redirect-uri
/// serialization are internal formats that shouldn't be hand-rolled. The tenant row, sample route
/// plans and the product catalogue come from DbUp scripts instead. Gated behind Seed:DevData;
/// never runs unless that's explicitly set.
/// </summary>
public static class DevDataSeeder
{
    public static readonly Guid DevTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string AuroraClientId = "aurora-spa";
    private const string FreightOpsClientId = "freightops-spa";
    private const string HubClientId = "hub-spa";

    private const string AdminEmail = "admin@aurora.local";
    private const string AdminPassword = "ChangeMe!Dev123";

    // One role per product. The dev admin gets admin rights in all three so the launcher shows
    // every tile; a real customer would hold only the roles for what they bought.
    private static readonly string[] AdminRoles = ["aurora:Admin", "fo:Admin", "hub:Admin"];

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var configuration = provider.GetRequiredService<IConfiguration>();
        var dbContext = provider.GetRequiredService<AuroraDbContext>();
        var applicationManager = provider.GetRequiredService<IOpenIddictApplicationManager>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await dbContext.Tenants.FindAsync(DevTenantId) is null)
        {
            throw new InvalidOperationException(
                "Dev tenant not found — DbUp script 0007_seed_dev_tenant_and_routes.sql should have run first.");
        }

        // Where each product actually lives. FreightOps is per-customer, so its URL is stored
        // against the tenant rather than being a single global setting.
        var auroraUrl = configuration["Products:AuroraUrl"] ?? "https://localhost:7259";
        var freightOpsUrl = configuration["Products:FreightOpsUrl"] ?? "http://localhost:5173";
        var hubUrl = configuration["Products:HubUrl"] ?? "https://localhost:7146";

        await EntitleDevTenantAsync(dbContext, auroraUrl, freightOpsUrl, hubUrl);
        await EnsureClientApplicationsAsync(applicationManager, auroraUrl, freightOpsUrl, hubUrl);
        await EnsureDevAdminUserAsync(dbContext, userManager, configuration,
            provider.GetRequiredService<IHostEnvironment>());

        Console.WriteLine($"Bootstrap account: {configuration["Seed:AdminEmail"] ?? AdminEmail}");
        Console.WriteLine($"Products: aurora={auroraUrl}  freightops={freightOpsUrl}  hub={hubUrl}");
    }

    private static async Task EntitleDevTenantAsync(
        AuroraDbContext dbContext, string auroraUrl, string freightOpsUrl, string hubUrl)
    {
        // Gives the dev tenant all three products. Removing a row here is the whole mechanism
        // for revoking a product: its roles stop being minted and its tile disappears.
        //
        // The tile URLs point at each product's SSO entry path rather than its root, so clicking
        // through from the launcher signs the user straight in instead of showing a login page.
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO tenant_product (tenant_id, product_code, instance_url)
            VALUES ({0}, 'aurora', {1}),
                   ({0}, 'freightops', {2} || '/auth/sso'),
                   ({0}, 'hub', {3} || '/sso')
            ON CONFLICT (tenant_id, product_code)
            DO UPDATE SET instance_url = EXCLUDED.instance_url, is_active = true;
            """,
            DevTenantId, auroraUrl, freightOpsUrl, hubUrl);
    }

    private static async Task EnsureClientApplicationsAsync(
        IOpenIddictApplicationManager applicationManager, string auroraUrl, string freightOpsUrl, string hubUrl)
    {
        // The deployed Aurora and a locally-run Aurora.Client are both registered, so pointing
        // Products:AuroraUrl at production does not stop the app running on a developer machine.
        await EnsureClientAsync(applicationManager, AuroraClientId, "Aurora",
            ["authentication/login-callback"], ["authentication/logout-callback"],
            auroraUrl, "https://localhost:7259");

        // The FreightOps Vue app and the Hub's Blazor client sign in here rather than against
        // their own login endpoints. Both are public clients — a browser app cannot keep a
        // secret — so PKCE is mandatory, and consent is implicit because these are first-party.
        await EnsureClientAsync(applicationManager, FreightOpsClientId, "FreightOps",
            ["auth/callback"], ["auth/logout-callback"], freightOpsUrl);

        // Both the deployed Hub and a locally-run Hub.Client are registered, so the same Aurora
        // can serve a real deployment and a developer machine without being re-seeded.
        await EnsureClientAsync(applicationManager, HubClientId, "Integration Hub",
            ["authentication/login-callback"], ["authentication/logout-callback"],
            hubUrl, "https://localhost:7146");
    }

    private static async Task EnsureClientAsync(
        IOpenIddictApplicationManager applicationManager,
        string clientId,
        string displayName,
        string[] redirectPaths,
        string[] postLogoutPaths,
        params string[] origins)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ApplicationType = ApplicationTypes.Web,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = displayName,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange }
        };

        foreach (var origin in origins.Where(o => !string.IsNullOrWhiteSpace(o)).Distinct())
        {
            var root = origin.TrimEnd('/');
            foreach (var path in redirectPaths)
            {
                descriptor.RedirectUris.Add(new Uri($"{root}/{path}"));
            }

            foreach (var path in postLogoutPaths)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri($"{root}/{path}"));
            }
        }

        var existing = await applicationManager.FindByClientIdAsync(clientId);
        if (existing is null)
        {
            await applicationManager.CreateAsync(descriptor);
        }
        else
        {
            // Keeps redirect URIs in step when a product URL changes between runs.
            await applicationManager.UpdateAsync(existing, descriptor);
        }
    }

    private static async Task EnsureDevAdminUserAsync(AuroraDbContext dbContext,
        UserManager<ApplicationUser> userManager, IConfiguration configuration, IHostEnvironment environment)
    {
        var email = configuration["Seed:AdminEmail"] ?? AdminEmail;
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            var password = configuration["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password))
            {
                password = environment.IsDevelopment() ? AdminPassword
                    : throw new InvalidOperationException("Set Seed:AdminPassword for initial server provisioning.");
            }
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            if (Guid.TryParse(configuration["Seed:AdminUserId"], out var userId)) user.Id = userId;

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create dev admin user: {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }
        }

        if (!await dbContext.UserTenants.AnyAsync(ut => ut.UserId == user.Id && ut.TenantId == DevTenantId))
        {
            dbContext.UserTenants.Add(new UserTenant
            {
                TenantId = DevTenantId,
                UserId = user.Id,
                IsDefault = true,
                JoinedUtc = DateTimeOffset.UtcNow
            });

            // Committed before the role assignments below: AspNetUserRoles' composite FK
            // requires the (tenant_id, user_id) row in user_tenant to already exist.
            await dbContext.SaveChangesAsync();
        }

        foreach (var roleName in AdminRoles)
        {
            // The roles themselves are created by DbUp script 0009, so this only assigns them.
            var role = await dbContext.Roles.SingleOrDefaultAsync(r => r.Name == roleName)
                ?? throw new InvalidOperationException(
                    $"Role '{roleName}' is missing — script 0009 should have created it.");

            if (await dbContext.Set<ApplicationUserRole>()
                .AnyAsync(ur => ur.UserId == user.Id && ur.TenantId == DevTenantId && ur.RoleId == role.Id))
            {
                continue;
            }

            dbContext.Add(new ApplicationUserRole
            {
                TenantId = DevTenantId,
                UserId = user.Id,
                RoleId = role.Id
            });
            await dbContext.SaveChangesAsync();
        }
    }
}
