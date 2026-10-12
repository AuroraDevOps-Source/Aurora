namespace Aurora.Contracts;

public sealed record DataRequirementIssue(string Code, string Message, string? ActionUrl = null, string? ActionLabel = null);

// Shared by planning eligibility and record grids so requirements have one definition.
public static class DataRequirements
{
    public static bool HasCoordinates(double? latitude, double? longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && (latitude != 0 || longitude != 0);

    public static DataRequirementIssue[] Depot(string code, double? latitude, double? longitude) =>
        HasCoordinates(latitude, longitude) ? [] :
        [new("depot-coordinates", $"Depot {code} needs valid latitude and longitude before it can be used for planning. Open this depot in Transportation Nodes, enter its coordinates, and save.", "aurora/terminals", "Open Transportation Nodes")];

    public static DataRequirementIssue[] Fleet(string terminal, bool terminalExists, double? latitude, double? longitude, double? weight, double? cubes)
    {
        var issues = new List<DataRequirementIssue>();
        if (string.IsNullOrWhiteSpace(terminal)) issues.Add(new("fleet-terminal", "Assign a terminal code to this vehicle before using it for planning."));
        else if (!terminalExists) issues.Add(new("unknown-terminal", $"Terminal {terminal} does not exist. Assign a valid terminal code to this vehicle.", "aurora/terminals", "Open Transportation Nodes"));
        else issues.AddRange(Depot(terminal, latitude, longitude));
        if (!(weight > 0)) issues.Add(new("fleet-weight", "Enter a maximum weight greater than zero before using this vehicle for planning."));
        if (!(cubes > 0)) issues.Add(new("fleet-cubes", "Enter a cubic capacity greater than zero before using this vehicle for planning."));
        return issues.ToArray();
    }

    public static DataRequirementIssue[] Order(int deliveries, int missingCoordinates, string terminal, bool terminalExists)
    {
        var issues = new List<DataRequirementIssue>();
        if (!terminalExists) issues.Add(new("order-terminal", $"This order needs a valid terminal assignment{(string.IsNullOrWhiteSpace(terminal) ? "." : $"; {terminal} is not defined.")} Correct the assignment in Nova.", "aurora/terminals", "Open Transportation Nodes"));
        if (deliveries != 1) issues.Add(new("delivery-count", "Planning requires exactly one delivery stop. Review this order’s delivery stops in Nova."));
        if (missingCoordinates > 0) issues.Add(new("delivery-coordinates", "Enter valid latitude and longitude for each delivery stop in this order before planning."));
        return issues.ToArray();
    }
}
