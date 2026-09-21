using Aurora.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Data.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenant");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Name).HasColumnName("name").IsRequired();
        builder.Property(t => t.Slug).HasColumnName("slug").IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.CreatedUtc).HasColumnName("created_utc");

        builder.HasIndex(t => t.Slug).IsUnique();
    }
}
