using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aurora.Client.Pages.Shared;

public sealed class ApplicationErrorBoundary : ErrorBoundary
{
    [Inject] public ClientErrorReporter Reporter { get; set; } = default!;
    protected override async Task OnErrorAsync(Exception exception)
    {
        await base.OnErrorAsync(exception);
        await Reporter.Report("UI failure");
    }
}
