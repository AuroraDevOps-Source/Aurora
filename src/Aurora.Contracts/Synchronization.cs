namespace Aurora.Contracts;

public sealed record WorkspaceMode(bool Local);
public sealed record SynchronizationStatus(bool Enabled,string Source,long Pending,long Working,long Conflicts,DateTime? LastSuccess,DateTime? LastAttempt,string? Error);
public sealed record SynchronizationIssue(Guid Id,string EntityType,string LocalId,string State,int Attempts,string? Error);
