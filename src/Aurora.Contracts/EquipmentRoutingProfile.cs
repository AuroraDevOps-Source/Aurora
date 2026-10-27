namespace Aurora.Contracts;

public sealed record EquipmentRoutingProfile(string Profile = "USA_5_DELIVERY", int Revision = 0);
public sealed record EquipmentRoutingProfileEdit(string Profile, int Revision, string EquipmentRevision);

public static class EquipmentRoutingProfiles
{
    public static readonly IReadOnlyDictionary<string,string> Choices = new Dictionary<string,string>
    {
        ["USA_1_PICKUP"] = "Pickup truck",
        ["USA_5_DELIVERY"] = "Delivery / box truck",
        ["USA_8_SEMITRAILER_5AXLE"] = "Tractor / trailer - 5 axles"
    };

    public static void Validate(string profile)
    {
        if (!Choices.ContainsKey(profile ?? "")) throw new FormatException("Select a valid routing profile.");
    }
}
