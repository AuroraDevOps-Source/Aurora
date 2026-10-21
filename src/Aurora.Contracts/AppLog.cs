namespace Aurora.Contracts;

public sealed record AppLogEntry(Guid Id, DateTime OccurredUtc, string UserName, string Level,
    string Action, string Resource, int StatusCode, string Detail, string TraceId, long DurationMs)
{
    public string References { get; init; } = "";
}
public sealed record AppLogPage(AppLogEntry[] Records, bool HasMore);
public sealed record ClientLogError(string Kind, string Page);
