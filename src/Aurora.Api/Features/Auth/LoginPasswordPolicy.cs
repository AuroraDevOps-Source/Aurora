using Microsoft.AspNetCore.Identity;

namespace Aurora.Api.Features.Auth;

public static class LoginPasswordPolicy
{
    public static void Configure(PasswordOptions options)
    {
        options.RequiredLength = 1;
        options.RequiredUniqueChars = 1;
        options.RequireDigit = false;
        options.RequireNonAlphanumeric = false;
        options.RequireLowercase = false;
        options.RequireUppercase = false;
    }
}
