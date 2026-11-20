namespace Aurora.Contracts;

public sealed record OrderImportHistory(Guid Id, string FileName, DateTime CreatedAt,
    string Status, int ExtractedCount, int CreatedCount)
{
    // Dapper needs a parameterless constructor when mapping the added history columns.
    public OrderImportHistory() : this(Guid.Empty, "", default, "", 0, 0) { }
    public string? FailureReason { get; init; }
    public bool HasOrderFailures { get; init; }
    public int? SkippedCount { get; init; }
    public int? FailedCount { get; init; }
    public string[] CreatedPros { get; init; } = [];
}

public sealed record ImportFailure(Guid RowId, string OrderLabel, string Reason);
public sealed record ImportReviewCounts(int Skipped, int Failed, ImportFailure[]? ClientFailures = null);

public sealed record ImportedOrderEdit(string Revision, OrderDocumentDraft Draft, bool NativeWorkspace)
{
    public bool ClearSchedule {get;init;}
}
