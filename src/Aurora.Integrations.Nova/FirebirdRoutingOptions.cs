using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed class FirebirdRoutingOptions
{
    public bool Enabled { get; set; }
    public Guid TenantId { get; set; }
    public string ConnectionString { get; set; } = "";
    // Stable identity of this database, independent of credentials or hostname changes.
    public string SourceKey { get; set; } = "";
    public int ReadyStatus { get; set; } = 110;
    public string TimeZoneId { get; set; } = "America/New_York";
    public int ServiceSeconds { get; set; } = 900;
}
