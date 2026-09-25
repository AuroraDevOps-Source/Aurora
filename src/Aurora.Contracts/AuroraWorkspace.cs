namespace Aurora.Contracts;

public sealed record OrderSourceDto(Guid Id, string Name);
public sealed record WorkspaceOrderDto(string Id, DateTimeOffset ScheduledAt, string Customer, string City, string Status);
public sealed record CreateOrderDraftDto(Guid SourceId, string[] OrderIds);
public sealed record OrderDraftDto(Guid Id, Guid SourceId, string FileName, string RequestJson, bool Finished);
public sealed record FinishOrderDraftDto(Guid SessionId);
public sealed record SavedManifestDto(Guid Id, Guid DraftId, string Vehicle, DateTimeOffset CreatedAt, RouteSummaryDto Route, ManifestDetailsDto? Details = null, int Revision = 0);
public sealed record SeedOrdersDto(string Name, string RequestJson);

public sealed record WorkspaceTenantDto(Guid Id, string Name);

public sealed class ManifestDetailsDto
{
    public string BillToCode { get; set; } = "";
    public string BillToName { get; set; } = "";
    public string Carrier { get; set; } = "";
    public string Driver { get; set; } = "";
    public string Trailer { get; set; } = "";
    public string OriginTerminal { get; set; } = "";
    public string DestinationTerminal { get; set; } = "";
    public string Direction { get; set; } = "Outbound";
    public DateOnly? ManifestDate { get; set; }
    public string Notes { get; set; } = "";
    public void Validate()
    {
        if (Direction is not ("Outbound" or "Inbound")) throw new FormatException("Choose Inbound or Outbound.");
        if (new[] { BillToCode, BillToName, Carrier, Driver, Trailer, OriginTerminal, DestinationTerminal }.Any(s => s is null || s.Length > 200) || Notes is null || Notes.Length > 4000)
            throw new FormatException("Fields allow up to 200 characters; notes allow 4,000.");
    }
}
public sealed record UpdateManifestDto(ManifestDetailsDto Details, int Revision);
