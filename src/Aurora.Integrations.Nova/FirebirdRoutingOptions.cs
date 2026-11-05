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
    // Worker-only: keep transport snapshots separate from direct Firebird planning snapshots.
    public bool IntegrationStaging { get; set; }
    public Sync.LykesNovaOptions Lykes { get; set; } = new();
    // Host defaults must never enable this tenant-specific profile on another database.
    public Sync.LykesNovaOptions LykesForSource(Guid tenantId,string sourceKey) =>
        TenantId!=Guid.Empty&&TenantId==tenantId&&!string.IsNullOrWhiteSpace(SourceKey)&&SourceKey==sourceKey
            ? Lykes : new();
}
