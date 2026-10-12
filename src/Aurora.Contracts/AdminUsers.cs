namespace Aurora.Contracts;

public sealed record AdminUserDto(Guid Id,string UserName,string Email,string Level,string Revision,
    string NovaHost,int NovaPort,string NovaDatabase,string NovaUserName,bool HasNovaPassword,bool CanEdit);
public sealed record AdminUserEdit(string UserName,string Email,string Level,string? LoginPassword,string Revision,
    string NovaHost,int NovaPort,string NovaDatabase,string NovaUserName,string? NovaPassword);
