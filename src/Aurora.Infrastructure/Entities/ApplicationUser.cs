using Microsoft.AspNetCore.Identity;

namespace Aurora.Infrastructure.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool InvitationPending { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("Aurora_User")]
    public bool AuroraUser { get; set; }
}
