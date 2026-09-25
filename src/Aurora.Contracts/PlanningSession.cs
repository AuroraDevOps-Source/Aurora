namespace Aurora.Contracts;

public sealed record StartPlanningDto(Guid Id, string FileName, string RequestJson, string? PreviousResult, int DurationSeconds, bool KeepAssignments = false, Guid? WorkspaceDraftId = null);
public sealed record PlanningSampleDto(DateTimeOffset Time, int Scheduled, int Unscheduled, int Routes, double? Cost);
public sealed record PlanningSessionDto(Guid Id, string Status, string FileName, string RequestJson, double ElapsedSeconds,
    IReadOnlyList<PlanningSampleDto> Samples, OptimizationResultDto? Result, string? Warning = null);
