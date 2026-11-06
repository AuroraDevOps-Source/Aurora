using Aurora.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aurora.Api.Features.Auth;

public static class FirstLogin
{
    public const string TemporaryPassword = "AuroraTemp123";
    private static string Purpose(ApplicationUser user) => "FirstLogin:" + user.NormalizedEmail;

    public static Task<string> Token(UserManager<ApplicationUser> users, ApplicationUser user) =>
        users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, Purpose(user));

    public static async Task<IdentityResult> Complete(UserManager<ApplicationUser> users, ApplicationUser user,
        string temporaryPassword, string token, string newPassword)
    {
        if (!user.InvitationPending || temporaryPassword != TemporaryPassword ||
            !await users.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, Purpose(user), token))
            return Error("Check the temporary password and invitation link, or ask your administrator to resend the invitation.");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword == TemporaryPassword)
            return Error("Choose a personal password different from the temporary password.");

        // AddPasswordAsync persists both the new hash and the flag with Identity's concurrency check.
        user.InvitationPending = false;
        return await users.AddPasswordAsync(user, newPassword);
    }

    private static IdentityResult Error(string message) => IdentityResult.Failed(new IdentityError { Description = message });
}
