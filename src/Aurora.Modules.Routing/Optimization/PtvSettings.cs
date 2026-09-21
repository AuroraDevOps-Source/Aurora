namespace Aurora.Modules.Routing.Optimization;

/// <summary>
/// PTV OptiFlow connection settings, bound from the Ptv configuration section.
///
/// Aurora calls PTV directly here, matching the tester this was ported from. The Integration Hub
/// also exposes a PTV gateway (<c>/api/ptv/...</c>) that keeps the credential server-side; moving
/// to it later means changing only <see cref="PtvClient"/>'s base address and auth header, which
/// is why the call is isolated in that one class.
/// </summary>
public sealed class PtvSettings
{
    public const string SectionName = "Ptv";

    public string BaseUrl { get; set; } = "https://api.myptv.com/routeoptimization/optiflow/v1";
    public string ApiKey { get; set; } = "";
    public int PollIntervalSeconds { get; set; } = 2;
    public int PollTimeoutSeconds { get; set; } = 180;
    public int RequestTimeoutSeconds { get; set; } = 60;
}
