namespace Aurora.Contracts;

// Matches Nova 2.0 ShipmentStatus codes; the requested Aurora display name is Ready to Ship.
public static class WorkspaceOrderStatus
{
    public const string Ready = "ReadyToRoute";
    public const string Routed = "Routed";
    public static readonly string[] All = ["Pending", Ready, "AwaitingAppointment", Routed, "Dispatched", "InTransit", "OutForDelivery", "Delivered", "Voided"];
    public static string Label(string value) => value switch
    {
        Ready => "Ready to Ship", "AwaitingAppointment" => "Awaiting Appointment", "InTransit" => "In Transit",
        "OutForDelivery" => "Out for Delivery", _ => value
    };
}
