namespace Aurora.Contracts;

public sealed record AdminUserDto(Guid Id,string UserName,string Email,string Level,string Revision,
    bool CanEdit,Guid? DatabaseId,string DatabaseName,string? NovaUserId=null,bool NovaApiLogin=false);
public sealed record AdminUserEdit(string UserName,string Email,string Level,string? LoginPassword,string Revision,Guid? DatabaseId,string? NovaUserId=null,bool NovaApiLogin=false,bool SendInvitation=true);
public sealed record InvitationEmailResult(bool Sent, string? Error);
public sealed record AdminUserCreated(Guid Id, InvitationEmailResult Invitation);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AdminDatabaseDto(Guid Id,string Name,int Revision,string Host,int Port,string Database,string UserName,bool HasPassword,int UserCount);
public sealed record AdminDatabaseEdit(string Name,int Revision,string Host,int Port,string Database,string UserName,string? Password);
