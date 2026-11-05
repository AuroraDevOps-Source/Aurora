using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public static class OptimizationLabor
{
    public static void Validate(decimal rate)
    {
        if (rate is < 0 or > 99999.9999m || decimal.Round(rate, 4) != rate)
            throw new FormatException("Enter a nonnegative driver cost per hour with up to four decimal places.");
    }

    public static void ClearDriverAssignments(JsonObject root)
    {
        root["reporting"] ??= new JsonObject();
        var report = root["reporting"]!.AsObject();
        report.Remove("drivers");
        report.Remove("driverCostBasis");
        report.Remove("driverExclusions");
    }

    // Call after restoring saved equipment rates; never add labor to a previous combined rate.
    public static string Apply(string json, decimal rate)
    {
        Validate(rate);
        var root = RoutingInput.Parse(json);
        ClearDriverAssignments(root);
        foreach (var truck in RoutingInput.Items(root["vehicles"]))
            truck["costs"]!["perHour"] = truck["costs"]!["perHour"]!.GetValue<double>() + (double)rate;
        root["reporting"]!["driverCostPerHour"] = rate;
        return root.ToJsonString();
    }
}
