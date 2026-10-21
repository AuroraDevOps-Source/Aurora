namespace Aurora.Contracts;

public static class PlanningCandidateSelection
{
    // Miles are a constant multiple of kilometers; compare unrounded provider distances.
    public static PlanningCandidateDto? LeastMiles(IEnumerable<PlanningCandidateDto> candidates) => candidates
        .Where(c => double.IsFinite(c.Summary.Kilometers) && c.Summary.Kilometers >= 0)
        .OrderBy(c => c.Summary.Kilometers)
        .ThenBy(c => c.Id, StringComparer.Ordinal)
        .FirstOrDefault();
}
