namespace Aurora.Contracts;

public sealed record NovaRecordSummary(long Id, string Number, string Description, string Terminal, string Status)
{
    public DataRequirementIssue[] Issues { get; init; } = [];
}
public sealed record NovaRecordPage(NovaRecordSummary[] Records, bool HasMore);
public sealed record NovaRecordOptions(string[] Terminals, NovaManifestStatus[] Statuses);
public sealed record NovaField(string Key, string Label, string Kind, int MaxLength, bool ReadOnly, string? Value);
public sealed record NovaRecordPart(string Key, string Title, NovaField[] Fields);
public sealed record NovaRecordDocument(long Id, string Title, string Revision, NovaRecordPart[] Parts);
public sealed record NovaRecordEdit(string Revision, Dictionary<string, Dictionary<string,string?>> Values);
