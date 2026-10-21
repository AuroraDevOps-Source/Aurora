namespace Aurora.Contracts;

public sealed record NovaDispatchEdit(string Revision, long DriverId, long TruckId);
public sealed record NovaManifestNotesEdit(string Revision, string Notes);
public sealed record NovaManifestStatusEdit(string Revision, string StatusCode, long DriverId, long TruckId, DateTime? OccurredAt);
public sealed record NovaManifestHistory(DateTime? RecordedAt, DateTime? OccurredAt, string Operator, string PreviousStatus, string Status);
public sealed record NovaDispatchOption(long Id, string Name, string Terminal, string? Unavailable);
public sealed record NovaDispatchDocument(long ManifestId, string Revision, string Status, DateTime? Date,
    string Terminal, long? DriverId, long? TruckId, long? PlannedTruckId, string? PlannedTruck,
    bool CanEdit, bool Dispatched, DateTime? DispatchedAt, NovaDispatchOption[] Drivers,
    NovaDispatchOption[] Trucks, string[] Issues)
{
    public string StatusCode { get; init; } = "";
    public string ManifestType { get; init; } = "Delivery";
    public string Origin { get; init; } = "";
    public string Destination { get; init; } = "";
    public string Notes { get; init; } = "";
    public bool CanEditNotes { get; init; }
    public int OrderCount { get; init; }
    public RouteSummaryDto? PlannedRoute { get; init; }
    public string[] AllowedStatuses { get; init; } = [];
    public DateTime? DepartedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public string TimeZone { get; init; } = "";
    public string? CloseIssue { get; init; }
    public NovaManifestHistory[] History { get; init; } = [];
}
