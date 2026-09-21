using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public sealed class EquipmentTypeDto
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public double? Weight { get; set; }
    public double? Cubes { get; set; }
    public double? Pallets { get; set; }
    public double? InteriorLength { get; set; }
    public double? InteriorWidth { get; set; }
    public double? InteriorHeight { get; set; }
    public double? ExteriorLength { get; set; }
    public double? ExteriorWidth { get; set; }
    public double? ExteriorHeight { get; set; }
    public double? LengthWithPower { get; set; }
    public int? Axles { get; set; }
    public string Accessories { get; set; } = "";
    public string DoorType { get; set; } = "";
    public string CargoType { get; set; } = "";
    public string TruckClass { get; set; } = "";
    public string Cdl { get; set; } = "";
    public string Model { get; set; } = "";
    public string LoadPosition { get; set; } = "";
    public string Categories { get; set; } = "";
    public string RoutingProfile { get; set; } = "USA_5_DELIVERY";
    public int? MaximumStops { get; set; }
    public double? FixedCost { get; set; }
    public double? PerHour { get; set; }
    public double? PerKilometer { get; set; }
    public double? PerStop { get; set; }
    public string Notes { get; set; } = "";
    public bool PowerOnly { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Code) || Code.Length > 80 || string.IsNullOrWhiteSpace(Description))
            throw new FormatException("Equipment needs a type code (up to 80 characters) and description.");
        if (string.IsNullOrWhiteSpace(RoutingProfile)) throw new FormatException("A routing profile is required.");
        if (MaximumStops is < 1 || Axles is < 1) throw new FormatException("Stops and axles must be positive when provided.");
        if (new[] { Weight, Cubes, Pallets, InteriorLength, InteriorWidth, InteriorHeight, ExteriorLength, ExteriorWidth, ExteriorHeight, LengthWithPower, FixedCost, PerHour, PerKilometer, PerStop }
            .Any(x => x is not null && (!double.IsFinite(x.Value) || x < 0)))
            throw new FormatException("Dimensions, capacities and costs must be finite, non-negative numbers.");
    }
}

public sealed class EquipmentUnitDto
{
    public string Id { get; set; } = "";
    public string Alias { get; set; } = "";
    public string Terminal { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public bool Available { get; set; } = true;
    public string Vin { get; set; } = "";
    public string Plate { get; set; } = "";
    public string PlateState { get; set; } = "";
    public string Notes { get; set; } = "";
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 80 || string.IsNullOrWhiteSpace(Terminal) || string.IsNullOrWhiteSpace(TypeCode))
            throw new FormatException("Equipment needs an ID (up to 80 characters), terminal and type.");
    }
}

public sealed record EquipmentCatalogDto(IReadOnlyList<EquipmentTypeDto> Types, IReadOnlyList<EquipmentUnitDto> Units);

public static class EquipmentPlanning
{
    // Only map fields whose optimizer semantics are established. Dimensions/CDL remain catalog data.
    public static JsonObject Vehicle(EquipmentTypeDto type, string id, string depot, string start, string end)
    {
        type.Validate();
        if (type.PowerOnly) throw new FormatException("A power-only tractor needs a trailer. Choose a truck or trailer capacity configuration for optimization.");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(depot)) throw new FormatException("Choose a truck ID and depot.");
        var begin = RoutingInput.Time(JsonValue.Create(start));
        var finish = RoutingInput.Time(JsonValue.Create(end));
        if (begin is null || finish is null || finish <= begin) throw new FormatException("Return must be after departure, with explicit UTC offsets.");
        var loads = new JsonArray();
        void Load(string dimension, double? value) { if (value is not null) loads.Add(new JsonObject { ["dimension"] = dimension, ["value"] = value }); }
        Load("weight", type.Weight); Load("volume", type.Cubes); Load("pallets", type.Pallets);
        var costs = new JsonObject();
        void Cost(string key, double? value) { if (value is not null) costs[key] = value; }
        Cost("fixed", type.FixedCost); Cost("perHour", type.PerHour); Cost("perKilometer", type.PerKilometer); Cost("perStop", type.PerStop);
        var constraints = new JsonObject();
        if (loads.Count > 0) constraints["maximumLoads"] = loads;
        if (type.MaximumStops is not null) constraints["route"] = new JsonObject { ["maximumNumberOfStops"] = type.MaximumStops };
        var vehicle = new JsonObject
        {
            ["id"] = id.Trim(),
            ["start"] = new JsonObject { ["locationId"] = depot, ["earliestStartTime"] = start },
            ["end"] = new JsonObject { ["locationId"] = depot, ["latestEndTime"] = end },
            ["routing"] = new JsonObject { ["profile"] = type.RoutingProfile, ["trafficMode"] = "AVERAGE" },
            ["constraints"] = constraints,
            ["costs"] = costs
        };
        var categories = (type.Categories ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        if (categories.Length > 0) vehicle["categories"] = new JsonArray(categories.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        return vehicle;
    }
}
