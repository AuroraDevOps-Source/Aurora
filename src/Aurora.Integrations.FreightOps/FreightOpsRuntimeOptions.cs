namespace Aurora.Integrations.FreightOps;

// Machine-sync credentials are distinct from the legacy FreightOps user login.
// Bind from deployment secrets/environment, never send AccessToken to a client.
public sealed class FreightOpsRuntimeOptions
{
    public bool Enabled {get;set;}
    public FreightOpsRuntimeConnection[] Connections {get;set;}=[];
    public FreightOpsRuntimeConnection Resolve(Guid tenantId,Guid reference,string? sourceIdentity=null)
    {
        var matches=(Connections??[]).Where(x=>x is not null&&x.TenantId==tenantId&&x.CredentialReference==reference).ToArray();
        if(matches.Length!=1)throw new FormatException("Configure exactly one FreightOps machine connection for this tenant and credential reference.");
        var value=matches[0];value.Validate();
        if(sourceIdentity is not null&&sourceIdentity!=value.SourceIdentity)
            throw new FormatException("FreightOps source generation changed; reconcile before reconnecting.");
        return value;
    }
}
public sealed class FreightOpsRuntimeConnection
{
    public bool ManifestPublishingEnabled {get;set;}
    public Guid TenantId {get;set;}
    public Guid CredentialReference {get;set;}
    public string ApiUrl {get;set;}="";
    public string SourceIdentity {get;set;}="";
    public string AccessToken {get;set;}="";
    public void Validate()
    {
        if(TenantId==Guid.Empty||CredentialReference==Guid.Empty)throw new FormatException("Tenant and credential reference are required.");
        FreightOpsProtocol.Text(SourceIdentity,"source identity");
        if(!Uri.TryCreate(ApiUrl,UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||
            (uri.Scheme!="https"&&!(uri.Scheme=="http"&&uri.IsLoopback)))throw new FormatException("FreightOps requires HTTPS, except for loopback tests.");
        if(string.IsNullOrWhiteSpace(AccessToken)||AccessToken.Any(char.IsControl))throw new FormatException("Configure a FreightOps machine access token.");
    }
}
