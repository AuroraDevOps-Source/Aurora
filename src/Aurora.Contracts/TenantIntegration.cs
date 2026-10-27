namespace Aurora.Contracts;

public sealed record TenantIntegration(Guid Id,string Provider,Guid CredentialReference,string SourceIdentity,bool Enabled,int Revision);
public sealed record TenantIntegrationEdit(string Provider,Guid CredentialReference,bool Enabled,int Revision);
