using Aurora.Contracts;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace Aurora.Api.Features.Auth;

public static class AccountPassword
{
    public static void MapAccountPasswordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/me/password", Change).RequireAuthorization();
    }

    public static async Task<IResult> Change(ChangePasswordRequest input, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn, ITenantContext tenant)
    {
        if (string.IsNullOrEmpty(input.CurrentPassword) || string.IsNullOrEmpty(input.NewPassword))
            return Results.BadRequest(new ApiErrorDto("Enter your current password and a new password."));
        if (input.CurrentPassword == input.NewPassword)
            return Results.BadRequest(new ApiErrorDto("Choose a different password from your current password."));
        var user = await users.FindByIdAsync(tenant.UserId.ToString());
        if (user is null) return Results.Unauthorized();
        // Apply the same lockout protection as login when checking the current password.
        var check = await signIn.CheckPasswordSignInAsync(user, input.CurrentPassword, lockoutOnFailure: true);
        if (!check.Succeeded)
            return Results.BadRequest(new ApiErrorDto("The current password is incorrect or this account is locked. Try signing in again."));
        var result = await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        return result.Succeeded ? Results.Ok() : Results.BadRequest(new ApiErrorDto(string.Join(" ", result.Errors.Select(e => e.Description))));
    }
}
