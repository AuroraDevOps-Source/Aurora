namespace Aurora.Contracts;

public sealed record NovaRecordSummary(long Id, string Number, string Description, string Terminal, string Status)
{
    public DataRequirementIssue[] Issues { get; init; } = [];
    public string WorkType {get;init;}="Delivery";
    public string? ReceivingTerminal { get; init; }
    public long[] ManifestNumbers { get; init; } = [];
}
public sealed record NovaRecordPage(NovaRecordSummary[] Records, bool HasMore);
public sealed record NovaRecordOptions(string[] Terminals, NovaManifestStatus[] Statuses)
{
    public NovaEquipmentType[] EquipmentTypes { get; init; } = [];
    public Dictionary<string, string> TerminalNames { get; init; } = [];
}
public sealed record NovaEquipmentType(long Id,string Name);
public sealed record NovaField(string Key, string Label, string Kind, int MaxLength, bool ReadOnly, string? Value);
public sealed record NovaRecordPart(string Key, string Title, NovaField[] Fields);
public sealed record NovaRecordDocument(long Id, string Title, string Revision, NovaRecordPart[] Parts)
{
    public string[] LocationWarnings { get; init; } = [];
}
public sealed record NovaRecordEdit(string Revision, Dictionary<string, Dictionary<string,string?>> Values)
{
    public string[] CoordinateParts { get; init; } = [];
}
public sealed record NovaOrderRouting(string? ReceivingTerminal, int Revision);
public sealed record NovaOrderRoutingEdit(string? ReceivingTerminal, int Revision, string OrderRevision);
