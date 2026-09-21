namespace Aurora.Contracts;

public sealed record RunSummaryDto(
    string Status,
    int RoutesUsed,
    int VehiclesOffered,
    int Scheduled,
    int Unscheduled,
    double Kilometers,
    int DriveSeconds,
    double Cost,
    int Violations,
    int Orders,
    int Locations,
    double CostPerOrder,
    double KilometersPerOrder,
    double OrdersPerRoute,
    string CapacityLabel);

public sealed record RouteSummaryDto(
    string Vehicle,
    int Orders,
    int Stops,
    double Kilometers,
    int DriveSeconds,
    double Cost,
    double LoadUtilization,
    double TimeUtilization,
    string Terminal,
    int? BudgetedSeconds,
    int EstimatedSeconds,
    int ManifestStops,
    double? Units,
    double? Weight,
    double? Cubes,
    IReadOnlyList<ManifestProDto> Pros,
    IReadOnlyList<RouteStopDto> RouteStops,
    DateTimeOffset? Departure = null,
    DateTimeOffset? Return = null);

public sealed record ManifestProDto(
    string ProNumber,
    int? StopNumber,
    string ShipToName,
    string Address1,
    string City,
    string State,
    string PostalCode,
    double? Units,
    double? Weight,
    double? Cubes,
    string BillType,
    int? AllottedStopSeconds,
    double? Latitude,
    double? Longitude,
    AppointmentInfoDto? Appointment = null,
    DateTimeOffset? Arrival = null,
    DateTimeOffset? Departure = null);

public sealed record RouteStopDto(
    string LocationId,
    double Latitude,
    double Longitude,
    DateTimeOffset? Arrival,
    DateTimeOffset? Departure,
    bool IsDepot,
    IReadOnlyList<string> OrderIds);

public sealed record DroppedOrderDto(
    string OrderId,
    double? Latitude,
    double? Longitude,
    string Reason,
    string ShipToName,
    string Address1,
    string City,
    string State,
    string PostalCode,
    double? Units,
    double? Weight,
    double? Cubes,
    string BillType,
    int? AllottedStopSeconds,
    AppointmentInfoDto? Appointment = null);

public sealed record OptimizationResultDto(
    string FileName,
    string? OptimizationId,
    DateTimeOffset OptimizedAt,
    string Terminal,
    string Status,
    int HttpStatusCode,
    string HttpStatus,
    double ElapsedSeconds,
    bool TimedOut,
    RunSummaryDto? Summary,
    IReadOnlyList<RouteSummaryDto> Routes,
    IReadOnlyList<DroppedOrderDto> DroppedOrders,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> RequestFacts,
    IReadOnlyList<string> StopAudit,
    IReadOnlyList<string> Log,
    string RawResponse,
    string? SummaryError,
    string? RouteDetailWarning);

public sealed record ApiErrorDto(string Error, IReadOnlyList<string>? Details = null);
