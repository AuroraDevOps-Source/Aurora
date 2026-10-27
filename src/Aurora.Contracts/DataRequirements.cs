namespace Aurora.Contracts;

public sealed record DataRequirementIssue(string Code, string Message, string? ActionUrl = null, string? ActionLabel = null);

// Shared by planning eligibility and record grids so requirements have one definition.
public static class DataRequirements
{
    public static DataRequirementIssue[] VehicleCosts(double? perHour, double? perMile)
    {
        var issues = new List<DataRequirementIssue>();
        if (perHour is null) issues.Add(new("fleet-hourly-cost", "Enter a cost per hour in the vehicle editor. Zero is allowed; blank is not planning-ready."));
        if (perMile is null) issues.Add(new("fleet-mile-cost", "Enter a cost per mile in the vehicle editor. Zero is allowed; blank is not planning-ready."));
        return issues.ToArray();
    }
    public static DataRequirementIssue[] Address(string? street, string? city, string? postalCode)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(street)) missing.Add("a street address");
        if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(postalCode)) missing.Add("a city or ZIP/postal code");
        return missing.Count == 0 ? [] : [new("address-incomplete", "Enter " + string.Join(" and ", missing) + ".")];
    }

    public static bool HasCoordinates(double? latitude, double? longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && (latitude != 0 || longitude != 0);

    public static DataRequirementIssue[] Depot(string code, double? latitude, double? longitude) =>
        HasCoordinates(latitude, longitude) ? [] :
        [new("depot-coordinates", $"Depot {code} has no verified map location. Open it in Terminals, check its street address, city, state and ZIP, then save to look up its location automatically.", "aurora/terminals", "Open Terminals")];

    public static DataRequirementIssue[] Fleet(string terminal, bool terminalExists, string? street, string? city, string? postalCode, double? weight, double? cubes)
    {
        var issues = new List<DataRequirementIssue>();
        if (string.IsNullOrWhiteSpace(terminal)) issues.Add(new("fleet-terminal", "Assign a terminal code to this vehicle before using it for planning."));
        else if (!terminalExists) issues.Add(new("unknown-terminal", $"Terminal {terminal} does not exist. Assign a valid terminal code to this vehicle.", "aurora/terminals", "Open Terminals"));
        else issues.AddRange(Address(street, city, postalCode).Select(issue => issue with { Message = $"Terminal {terminal}: {issue.Message}", ActionUrl = "aurora/terminals", ActionLabel = "Open Terminals" }));
        if (!(weight > 0)) issues.Add(new("fleet-weight", "Enter a maximum weight greater than zero before using this vehicle for planning."));
        if (!(cubes > 0)) issues.Add(new("fleet-cubes", "Enter a cubic capacity greater than zero before using this vehicle for planning."));
        return issues.ToArray();
    }

    public static DataRequirementIssue[] Order(int deliveries, int missingCoordinates, string terminal, bool terminalExists)
    {
        var issues = new List<DataRequirementIssue>();
        if (!terminalExists) issues.Add(new("order-terminal", $"This order needs a valid terminal assignment{(string.IsNullOrWhiteSpace(terminal) ? "." : $"; {terminal} is not defined.")} Correct the assignment in Nova.", "aurora/terminals", "Open Terminals"));
        if (deliveries != 1) issues.Add(new("delivery-count", "Planning requires exactly one delivery stop. Review this order’s delivery stops in Nova."));
        return issues.ToArray();
    }
}
