namespace Aurora.Contracts;

public sealed class NovaDriver
{
    public long Id {get;set;}
    public string Revision {get;set;}="";
    public string Number {get;set;}="";
    public string FirstName {get;set;}="";
    public string LastName {get;set;}="";
    public string Terminal {get;set;}="";
    public string Phone {get;set;}="";
    public string Email {get;set;}="";
    public DateTime? HireDate {get;set;}
    public DateTime? TerminationDate {get;set;}
    public DateTime? LicenseExpiry {get;set;}
    public long? TruckId {get;set;}
    public string Truck {get;set;}="";
    public string ShiftStart {get;set;}="";
    public string ShiftEnd {get;set;}="";
    public decimal? PerHour {get;set;}
    public string Name => string.Join(" ",new[]{FirstName,LastName}.Where(s=>!string.IsNullOrWhiteSpace(s)));
    public void Validate()
    {
        foreach(var (value,length,label) in new[]{(Number,14,"Driver number"),(FirstName,40,"First name"),(LastName,40,"Last name"),(Terminal,12,"Terminal"),(Phone,32,"Phone"),(Email,150,"Email")})
            if(value is null || value.Length>length)throw new FormatException($"{label} allows up to {length} characters.");
        if(string.IsNullOrWhiteSpace(Number)||string.IsNullOrWhiteSpace(LastName)||string.IsNullOrWhiteSpace(Terminal))throw new FormatException("Enter a driver number, last name and terminal.");
        if(PerHour is <0 or >99999.9999m || PerHour is {} rate && decimal.Round(rate,4)!=rate)throw new FormatException("Enter a nonnegative hourly labor cost with up to four decimal places.");
        if(TerminationDate<HireDate)throw new FormatException("Termination date cannot precede hire date.");
    }
}
public sealed record DriverTruck(long Id,string Number,string Terminal);
public sealed record DriverDirectory(NovaDriver[] Drivers,DriverTruck[] Trucks,string[] Terminals);
public static class DriverCostBasis
{
    public const string Separate="equipment-only", Included="driver-included";
    public static void Validate(string? value) { if(value is not (Separate or Included))throw new FormatException("Choose whether equipment rates already include driver pay."); }
}
