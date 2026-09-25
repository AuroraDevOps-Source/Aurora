using Aurora.Api.Auth;
using Aurora.Api.Features.Auth;
using Aurora.Api.Features.Launcher;
using Aurora.Api.Features.Routing;
using Aurora.Api.Middleware;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing.Optimization;
using Aurora.Modules.Routing.Queries.ListRoutePlans;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using static OpenIddict.Server.OpenIddictServerEvents;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// Deployment shape. Behind Cloudflare the origin sees plain HTTP on an internal host, so the
// scheme/host it would infer for itself are wrong — which matters because OpenIddict stamps the
// issuer into every token and publishes it in the discovery document.
var publicOrigin = builder.Configuration["Hosting:PublicOrigin"];
var behindReverseProxy = builder.Configuration.GetValue("Hosting:BehindReverseProxy", false);
var allowEmbeddedLogin = builder.Configuration.GetValue("Auth:AllowEmbeddedLogin", false);

if (builder.Configuration["Hosting:DataProtectionPath"] is { Length: > 0 } keyPath)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("Aurora")
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}

if (behindReverseProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedFor;

        // Cloudflare egresses from a large, changing set of addresses and the origin is only
        // reachable through it, so pinning known proxies buys nothing here.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// --- Npgsql data source (shared by EF Core and Dapper) ---
var connectionString = builder.Configuration.GetConnectionString("Aurora")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Aurora — set it via user-secrets.");
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(connectionString).Build());

// --- Tenancy plumbing ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<TenantConnectionInterceptor>();
builder.Services.AddScoped<NpgsqlConnectionFactory>();

// --- EF Core (writes) ---
builder.Services.AddDbContext<AuroraDbContext>((sp, options) =>
{
    options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>());
    options.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());
});

// --- Identity ---
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AuroraDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// AddIdentityCore + AddSignInManager register the *services* (SignInManager, etc.) but not an
// actual cookie authentication handler — that needs an explicit AddCookie call. This cookie is
// only ever checked by AuthorizationController.Authorize(); ordinary API requests authenticate
// against the bearer access token (OpenIddict validation), set as the default scheme below.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
})
.AddCookie(IdentityConstants.ApplicationScheme, options =>
{
    options.Cookie.Name = "Aurora.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // Product frames navigate back to this issuer during PKCE sign-in. That redirect is
    // cross-site even under Aurora's top-level page, so Lax withholds the session cookie.
    // Embedded deployments explicitly opt in to None + Secure; origin checks and same-origin
    // framing restrictions below protect the cookie-authenticated endpoints.
    options.Cookie.SameSite = allowEmbeddedLogin ? SameSiteMode.None : SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    // API, not a browsable site — a 302 to a login page makes no sense here.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
});

// --- OpenIddict ---
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<AuroraDbContext>().ReplaceDefaultEntities<Guid>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token")
               .SetUserInfoEndpointUris("connect/userinfo");

        // Pinned rather than inferred from the request. FreightOps and the Hub validate the
        // issuer against what discovery advertises, so if a proxy hop made Aurora think it was
        // http://internal-host every token it minted would be rejected downstream.
        if (!string.IsNullOrWhiteSpace(publicOrigin))
        {
            options.SetIssuer(new Uri(publicOrigin));
        }

        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.AllowRefreshTokenFlow();

        options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess);

        options.SetAccessTokenLifetime(
            TimeSpan.FromMinutes(builder.Configuration.GetValue("Auth:AccessTokenMinutes", 12)));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            var signingPath = builder.Configuration["Auth:SigningCertificatePath"]
                ?? throw new InvalidOperationException("Configure Auth:SigningCertificatePath.");
            var encryptionPath = builder.Configuration["Auth:EncryptionCertificatePath"]
                ?? throw new InvalidOperationException("Configure Auth:EncryptionCertificatePath.");
            options.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                signingPath, builder.Configuration["Auth:CertificatePassword"]));
            options.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                encryptionPath, builder.Configuration["Auth:CertificatePassword"]));
        }

        // FreightOps and the Hub validate these tokens with stock JWT bearer + discovery.
        // OpenIddict encrypts access tokens by default (JWE), which only an OpenIddict
        // validation handler can read — so they must be plain signed JWTs for other
        // applications to consume. Signing still guarantees authenticity; anything genuinely
        // secret simply must not be put in a claim.
        options.DisableAccessTokenEncryption();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .EnableTokenEndpointPassthrough();

        options.AddEventHandler<ApplyTokenResponseContext>(b =>
            b.UseSingletonHandler<MoveRefreshTokenToCookieHandler>());
        options.AddEventHandler<ExtractTokenRequestContext>(b =>
            b.UseSingletonHandler<InjectRefreshTokenFromCookieHandler>());
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthorization();

// --- CORS ---
// Aurora's own client needs credentialed cross-port requests for the login cookie. The
// FreightOps and Hub front ends also talk to this origin directly once they sign in through
// Aurora (the token exchange is an XHR, even though the authorize step is a redirect), so
// every product front end has to be listed here.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (builder.Configuration["Cors:ClientOrigin"] is { Length: > 0 } legacyOrigin)
{
    allowedOrigins = [.. allowedOrigins, legacyOrigin];
}

if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Configure Cors:AllowedOrigins (or Cors:ClientOrigin).");
}

builder.Services.AddCors(o => o.AddPolicy("wasm-client", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// --- Routing module ---
builder.Services.AddScoped<ListRoutePlansQuery>();
builder.Services.AddScoped<Aurora.Modules.Routing.EquipmentStore>();
builder.Services.AddScoped<Aurora.Modules.Routing.OrderWorkspace>();
builder.Services.Configure<PtvSettings>(builder.Configuration.GetSection(PtvSettings.SectionName));
builder.Services.AddHttpClient("PtvRoadRouting", client => client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<OptimizationService>();
builder.Services.AddSingleton<Aurora.Api.Features.Routing.PlanningSessions>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (behindReverseProxy)
{
    // Must run before anything reads the scheme or host.
    app.UseForwardedHeaders();
}
else
{
    // Skipped behind a proxy: with Cloudflare terminating TLS the origin receives HTTP, and
    // redirecting it to HTTPS just bounces the request straight back — an endless loop.
    // Enforce HTTPS at Cloudflare ("Always Use HTTPS") instead.
    app.UseHttpsRedirection();
}

// Chrome blocks a request from a public origin (a deployed FreightOps/Hub front end) to a
// private one (Aurora on localhost during a demo) unless the preflight opts in. Without this
// the redirect to /connect/authorize still works — it is a navigation — but the token exchange,
// which is a fetch, fails with an opaque CORS error.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsOptions(context.Request.Method) &&
        context.Request.Headers["Access-Control-Request-Private-Network"] == "true")
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseCors("wasm-client");
app.Use(async (context, next) =>
{
    // Only Aurora itself may frame its login/authorization pages. The product frame temporarily
    // navigates here during SSO, with Aurora still its sole ancestor.
    context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'self'";

    // SameSite=None is not a CSRF defense. Check the browser-controlled Origin on every
    // session-changing endpoint, including form POSTs that do not trigger CORS preflight.
    if (context.Request.Path.StartsWithSegments("/api/auth") &&
        !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
        context.Request.Headers.TryGetValue("Origin", out var origin) &&
        !allowedOrigins.Contains(origin.ToString(), StringComparer.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();
app.MapLoginEndpoints();
app.MapLauncherEndpoints();
app.MapRoutingEndpoints();
app.MapOrderWorkspaceEndpoints();

app.MapGet("/health/live", () => Results.Ok()).AllowAnonymous();
app.MapGet("/health/ready", async (AuroraDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous();

app.Run();
