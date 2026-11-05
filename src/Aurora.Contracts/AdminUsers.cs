namespace Aurora.Contracts;

public sealed record AdminUserDto(Guid Id,string UserName,string Email,string Level,string Revision,
    bool CanEdit);
public sealed record AdminUserEdit(string UserName,string Email,string Level,string? LoginPassword,string Revision,bool SendInvitation=true);
public sealed record InvitationEmailResult(bool Sent, string? Error);
public sealed record AdminUserCreated(Guid Id, InvitationEmailResult Invitation);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
