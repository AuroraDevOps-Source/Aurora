namespace Aurora.Contracts;

public sealed record OrderImportHistory(Guid Id, string FileName, DateTime CreatedAt,
    string Status, int ExtractedCount, int CreatedCount)
{
    // Dapper needs a parameterless constructor when mapping the added history columns.
    public OrderImportHistory() : this(Guid.Empty, "", default, "", 0, 0) { }
    public string[] CreatedPros { get; init; } = [];
}

public sealed record ImportedOrderEdit(string Revision, OrderDocumentDraft Draft, bool NativeWorkspace)
{
    public bool ClearSchedule {get;init;}
}
