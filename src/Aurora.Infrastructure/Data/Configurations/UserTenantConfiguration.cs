using Aurora.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Data.Configurations;

public sealed class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("user_tenant");
        builder.HasKey(ut => new { ut.TenantId, ut.UserId });

        builder.Property(ut => ut.TenantId).HasColumnName("tenant_id");
        builder.Property(ut => ut.UserId).HasColumnName("user_id");
        builder.Property(ut => ut.IsDefault).HasColumnName("is_default");
        builder.Property(ut => ut.JoinedUtc).HasColumnName("joined_utc");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(ut => ut.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(ut => ut.UserId).OnDelete(DeleteBehavior.Cascade);

        // Exactly one default tenant per user.
        builder.HasIndex(ut => ut.UserId)
            .HasDatabaseName("ix_user_tenant_one_default_per_user")
            .IsUnique()
            .HasFilter("is_default = true");
    }
}
