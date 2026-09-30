namespace Aurora.Contracts;

public sealed class CustomerDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Address2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public int Revision { get; set; }
    public void Validate()
    {
        Code=Code?.Trim() ?? "";Name=Name?.Trim() ?? "";
        if(Code.Length is 0 or >80 || Name.Length is 0 or >200) throw new FormatException("Customer code (up to 80 characters) and name (up to 200) are required.");
        if(new[]{Address,Address2,City,State,PostalCode,Country,Phone,Email}.Any(v=>v is null || v.Length>200)) throw new FormatException("Customer fields allow up to 200 characters.");
    }
}
