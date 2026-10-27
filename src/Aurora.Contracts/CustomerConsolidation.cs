namespace Aurora.Contracts;

public sealed record CustomerConsolidation(string Customer,string AddressKey,string EquipmentType,decimal MaximumWeight,decimal MaximumCubes,bool Held,Dictionary<long,decimal> EstimatedCubes);
public sealed record ConsolidationRelease(string Revision);
public sealed record ConsolidationProgress(string Customer,bool Held,decimal Weight,decimal Cubes,decimal MaximumWeight,decimal MaximumCubes,int EstimatedOrders)
{
    public bool Full=>Weight>=MaximumWeight||Cubes>=MaximumCubes;
}

public static class ConsolidationCapacity
{
    public static bool Fits(decimal weight,decimal cubes,decimal addedWeight,decimal addedCubes,decimal maxWeight,decimal maxCubes)
        =>addedWeight>0&&addedCubes>0&&weight>=0&&cubes>=0&&weight+addedWeight<=maxWeight&&cubes+addedCubes<=maxCubes;
    public static string AddressKey(string? address,string? city,string? state,string? zip)
        =>string.Join("|",new[]{address,city,state,zip}.Select(s=>System.Text.RegularExpressions.Regex.Replace((s??"").Trim().ToUpperInvariant(),@"\s+"," ")));
}
