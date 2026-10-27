namespace Aurora.Contracts;
public static class PickupCapacity
{
    // Start with deliveries aboard. Pickups add freight; deliveries remove it.
    public static (double Weight,double Cubes) Peak(IEnumerable<(bool Pickup,double Weight,double Cubes)> orderedStops)
    {
        var stops=orderedStops.ToArray();
        if(stops.Any(s=>!double.IsFinite(s.Weight) || !double.IsFinite(s.Cubes) || s.Weight<0 || s.Cubes<0))throw new FormatException("Freight quantities cannot be negative.");
        var weight=stops.Where(s=>!s.Pickup).Sum(s=>s.Weight);
        var cubes=stops.Where(s=>!s.Pickup).Sum(s=>s.Cubes);
        var maxWeight=weight;var maxCubes=cubes;
        foreach(var stop in stops){var direction=stop.Pickup?1:-1;weight+=direction*stop.Weight;cubes+=direction*stop.Cubes;maxWeight=Math.Max(maxWeight,weight);maxCubes=Math.Max(maxCubes,cubes);}
        return(maxWeight,maxCubes);
    }
}
