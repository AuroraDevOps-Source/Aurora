namespace Aurora.Contracts;
public sealed record NovaMasterSummary(string Id,string Name,string City)
{
    public DataRequirementIssue[] Issues { get; init; } = [];
}
public sealed record NovaMasterPage(NovaMasterSummary[] Records,bool HasMore);
public sealed record NovaMasterDocument(string Id,string Revision,NovaField[] Fields)
{
    public string? LocationWarning { get; init; }
}
public sealed record NovaMasterEdit(string Revision,Dictionary<string,string?> Values);
