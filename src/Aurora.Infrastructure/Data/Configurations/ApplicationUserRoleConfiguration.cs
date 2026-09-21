using Aurora.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Data.Configurations;

public sealed class ApplicationUserRoleConfiguration : IEntityTypeConfiguration<ApplicationUserRole>
{
    public void Configure(EntityTypeBuilder<ApplicationUserRole> builder)
    {
        // Overrides IdentityDbContext's default (UserId, RoleId) key — role assignment
        // is per-tenant here, not global (§6.2 / token contract "roles within that tenant").
        builder.HasKey(ur => new { ur.TenantId, ur.UserId, ur.RoleId });

        builder.HasOne<UserTenant>()
            .WithMany()
            .HasForeignKey(ur => new { ur.TenantId, ur.UserId })
            .HasPrincipalKey(ut => new { ut.TenantId, ut.UserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
