using Aurora.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;

namespace Aurora.Api.Features.Auth;

public static class LoginEndpoints
{
    public static void MapLoginEndpoints(this IEndpointRouteBuilder app)
    {
        // Plain Identity password check that sets the pre-authentication cookie —
        // OpenIddict never sees the password. The client redirects to /connect/authorize
        // immediately after this succeeds (see AuthorizationController.Authorize).
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByNameAsync(request.UserName);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return Results.Unauthorized();
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return Results.Ok();
        }).AllowAnonymous();

        app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Ok();
        });

        // Re-issue the existing session with current cookie attributes before launching an
        // embedded product. This upgrades already signed-in browsers without asking for their
        // password again; the original principal and expiration are preserved. Authenticate
        // explicitly here: the pre-auth cookie has no tenant and must not become the ambient
        // API identity consumed by tenant-resolution middleware.
        app.MapPost("/api/auth/prepare-embedded-session", async (HttpContext context) =>
        {
            var session = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!session.Succeeded || session.Principal is null)
                return Results.Unauthorized();
            await context.SignInAsync(IdentityConstants.ApplicationScheme, session.Principal, session.Properties);
            return Results.Ok();
        }).AllowAnonymous();
    }
}

public sealed record LoginRequest(string UserName, string Password);
