using System.Net;
using System.Text.Json;
using Aurora.Api.Features.Auth;
using Aurora.Contracts;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS " + name); checks++;
}
var handler = new RecordingHandler();
using var http = new HttpClient(handler);
var settings = new Dictionary<string, string?> { ["RESEND_API_KEY"] = "re_test_only", ["Resend:PublicOrigin"] = "https://client.example" };
ResendInvitationEmail Sender(Dictionary<string, string?> config) => new(http, new ConfigurationBuilder().AddInMemoryCollection(config).Build(), NullLogger<ResendInvitationEmail>.Instance);
var id = Guid.NewGuid();
var result = await Sender(settings).Send(id, "<Alice & Bob>", "alice@example.invalid", default);
Check(result.Sent && result.Error is null, "accepted invitation reports success");
Check(handler.Url == "https://api.resend.com/emails" && handler.Method == HttpMethod.Post && handler.Authorization == "Bearer re_test_only", "Resend endpoint and server authentication");
Check(handler.IdempotencyKey?.StartsWith($"user-invitation/{id}/") == true, "invitation request has an idempotency key");
using (var payload = JsonDocument.Parse(handler.Body!))
{
    var body = payload.RootElement;
    Check(body.GetProperty("from").GetString() == "Aurora <alerts@aurorsoftware.com>" && body.GetProperty("to")[0].GetString() == "alice@example.invalid", "exact requested sender and recipient");
    Check(body.GetProperty("html").GetString()!.Contains("&lt;Alice &amp; Bob&gt;") && !body.GetProperty("html").GetString()!.Contains("<Alice"), "username HTML is escaped");
    Check(body.GetProperty("text").GetString()!.Contains("https://client.example/login?changePassword=true") && body.GetProperty("text").GetString()!.Contains("provided by your administrator"), "invitation links to password-change login and explains initial password");
    Check(!handler.Body!.Contains("re_test_only"), "API key does not leak into email content");
}
foreach (var origin in new[] { "https://client.example", "https://localhost:7259", "http://localhost:7259" })
    Check((await Sender(new(settings) { ["Resend:PublicOrigin"] = origin }).Send(id, "Alice", "alice@example.invalid", default)).Sent, "valid configured origin " + origin);
foreach (var origin in new[] { "", "https://client.example/path", "http://client.example", "javascript:alert(1)", "https://user:pass@client.example", "https://client.example/?next=evil" })
{
    var count = handler.Calls;
    Check(!(await Sender(new(settings) { ["Resend:PublicOrigin"] = origin }).Send(id, "Alice", "alice@example.invalid", default)).Sent && handler.Calls == count, "invalid origin rejected before sending");
}
var before = handler.Calls;
Check(!(await Sender(new(settings) { ["RESEND_API_KEY"] = "" }).Send(id, "Alice", "alice@example.invalid", default)).Sent && handler.Calls == before, "missing key returns actionable failure without sending");
Check((await Sender(new() { ["RESEND_API_KEY"] = "re_test_only", ["Hosting:PublicOrigin"] = "https://fallback.example" }).Send(id, "Alice", "alice@example.invalid", default)).Sent && handler.Body!.Contains("fallback.example"), "hosting origin fallback");
foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.UnprocessableEntity, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
{
    handler.Status = status;
    var failed = await Sender(settings).Send(id, "Alice", "alice@example.invalid", default);
    Check(!failed.Sent && failed.Error!.Contains("Send invitation") && !failed.Error.Contains("provider-secret"), "provider failure is actionable and sanitized: " + status);
}
foreach (var error in new Exception[] { new HttpRequestException("provider-secret"), new TaskCanceledException("provider-secret") })
{
    handler.Error = error;
    Check(!(await Sender(settings).Send(id, "Alice", "alice@example.invalid", default)).Sent, "network/timeout failure preserves saved-user result");
}

// Exercise the real Identity hashing/change-password implementation without a database.
var identityOptions = Options.Create(new IdentityOptions());
LoginPasswordPolicy.Configure(identityOptions.Value.Password);
var store = new MemoryUserStore();
using var users = new UserManager<ApplicationUser>(store, identityOptions, new PasswordHasher<ApplicationUser>(),
    [new UserValidator<ApplicationUser>()], [new PasswordValidator<ApplicationUser>()], new UpperInvariantLookupNormalizer(),
    new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance);
var signIn = new SignInManager<ApplicationUser>(users, new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
    new UserClaimsPrincipalFactory<ApplicationUser>(users, identityOptions), identityOptions,
    NullLogger<SignInManager<ApplicationUser>>.Instance, new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())),
    new DefaultUserConfirmation<ApplicationUser>());
var account = new ApplicationUser { Id = id, UserName = "Alice", Email = "alice@example.invalid" };
Check((await users.CreateAsync(account, "initial-password")).Succeeded, "Identity test account created");
var tenant = new TestTenant(id);
async Task<int?> Change(string current, string next) => ((IStatusCodeHttpResult)await AccountPassword.Change(new(current, next), users, signIn, tenant)).StatusCode;
Check(await Change("", "new-password") == 400, "empty current password rejected");
Check(await Change("initial-password", "initial-password") == 400, "unchanged password rejected");
Check(await Change("wrong-password", "new-password") == 400 && await users.CheckPasswordAsync(account, "initial-password"), "incorrect current password cannot change credentials");
Check(await Change("initial-password", "new-password") == 200, "signed-in user can change own password");
Check(!await users.CheckPasswordAsync(account, "initial-password") && await users.CheckPasswordAsync(account, "new-password"), "initial password stops working and new password works");
tenant.UserIdOrNull = Guid.NewGuid();
Check(await Change("new-password", "other-password") == 401, "missing signed-in account cannot change another user's password");
Console.WriteLine($"{checks} invitation and password checks passed; no email was sent.");

sealed class RecordingHandler : HttpMessageHandler
{
    public int Calls;
    public string? Url, Authorization, IdempotencyKey, Body;
    public HttpMethod? Method;
    public HttpStatusCode Status = HttpStatusCode.OK;
    public Exception? Error;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++; Url = request.RequestUri!.AbsoluteUri; Method = request.Method;
        Authorization = request.Headers.Authorization?.ToString();
        IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
        Body = await request.Content!.ReadAsStringAsync(ct);
        if (Error is not null) throw Error;
        return new(Status) { Content = new StringContent(Status == HttpStatusCode.OK ? "{\"id\":\"test-message\"}" : "provider-secret") };
    }
}
sealed class TestTenant(Guid id) : ITenantContext
{
    public Guid? TenantIdOrNull => Guid.Empty;
    public Guid? UserIdOrNull { get; set; } = id;
}
sealed class MemoryUserStore : IUserPasswordStore<ApplicationUser>
{
    ApplicationUser? saved;
    public void Dispose() { }
    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) { saved = user; return Task.FromResult(IdentityResult.Success); }
    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult(saved?.Id.ToString() == id ? saved : null);
    public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult(saved?.NormalizedUserName == name ? saved : null);
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id.ToString());
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.UserName);
    public Task SetUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) { user.UserName = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) { user.NormalizedUserName = name; return Task.CompletedTask; }
    public Task SetPasswordHashAsync(ApplicationUser user, string? hash, CancellationToken ct) { user.PasswordHash = hash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash is not null);
}
