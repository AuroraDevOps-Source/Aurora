namespace Aurora.Contracts;

// A transportation node (workflow section 1.1). Terminals are one node type among several.
public sealed class TerminalDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
    public string NodeType { get; set; } = NodeVocabulary.Terminal;
    public string[] Functions { get; set; } = [];
    public string[] Services { get; set; } = [];
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string OperatingHours { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Region { get; set; } = "";
    public string RoutingRole { get; set; } = "";
    public string ServiceArea { get; set; } = "";
    public Guid? RouteViaNodeId { get; set; }
    public int Revision { get; set; }
    public void Validate()
    {
        Code = Code?.Trim() ?? "";
        Name = Name?.Trim() ?? "";
        if (Code.Length is 0 or > 80 || Name.Length is 0 or > 200)
            throw new FormatException("Node code (up to 80 characters) and name (up to 200) are required.");
        if (new[] { Address, City, State, PostalCode, Country, OperatingHours, Owner, Region }.Any(v => v is null || v.Length > 200))
            throw new FormatException("Node address, hours, owner, and region fields allow up to 200 characters.");
        if (!NodeVocabulary.Types.Contains(NodeType)) throw new FormatException("Choose a node type.");
        Functions ??= []; Services ??= [];
        if (Functions.Any(f => !NodeVocabulary.Functions.Contains(f)) || Services.Any(s => !NodeVocabulary.Services.Contains(s)))
            throw new FormatException("Choose node functions and special services from the list.");
        RoutingRole ??= "";
        if (RoutingRole.Length > 0 && !NodeVocabulary.Roles.Contains(RoutingRole)) throw new FormatException("Routing role must be Hub, Spoke, or Ad Hoc.");
        if (Latitude is null != Longitude is null || Latitude is < -90 or > 90 || Longitude is < -180 or > 180)
            throw new FormatException("Enter both latitude (-90 to 90) and longitude (-180 to 180), or neither.");
        if ((ServiceArea ?? "").Length > 4000) throw new FormatException("Service area allows up to 4000 characters.");
        var area = global::Aurora.Contracts.ServiceArea.Parse(ServiceArea);
        if (RoutingRole is NodeVocabulary.Hub or NodeVocabulary.Spoke && area.IsEmpty)
            throw new FormatException("A Hub or Spoke node needs a direct service area.");
        if (RouteViaNodeId == Id && Id != Guid.Empty) throw new FormatException("A node cannot route via itself.");
    }
}
