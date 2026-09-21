namespace Aurora.Infrastructure.Entities;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
}
