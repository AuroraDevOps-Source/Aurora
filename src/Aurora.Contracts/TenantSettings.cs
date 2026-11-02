namespace Aurora.Contracts;

public sealed class TenantSettingsDto
{
    public int Revision {get;set;}
    public string CompanyName {get;set;}="";
    public NovaConnectionSettings Nova {get;set;}=new();
    public FreightOpsConnectionSettings FreightOps {get;set;}=new();
    public DispatchTrackConnectionSettings DispatchTrack {get;set;}=new();
    public ZenventoryConnectionSettings Zenventory {get;set;}=new();
}
public sealed class NovaConnectionSettings
{
    public string Host {get;set;}="";
    public int Port {get;set;}=3050;
    public string Database {get;set;}="";
    public string UserName {get;set;}="";
    public string? Password {get;set;}
    public bool HasPassword {get;set;}
}
public sealed class FreightOpsConnectionSettings
{
    public string ApiUrl {get;set;}="";
    public string UserName {get;set;}="";
    public string? Password {get;set;}
    public bool HasPassword {get;set;}
}

public sealed class DispatchTrackConnectionSettings
{
 public string ApiUrl {get;set;}="";
 public string PostOrderUrl {get;set;}="";
 public string UpdateOrderUrl {get;set;}="";
 public string FetchOrderUrl {get;set;}="";
 public string ExportUrl {get;set;}="";
 public string? ApiCode {get;set;}
 public string? ApiKey {get;set;}
 public bool HasApiCode {get;set;}
 public bool HasApiKey {get;set;}
}
public sealed class ZenventoryConnectionSettings
{
 public string BaseUrl {get;set;}="";
 public string? ApiKey {get;set;}
 public string? ApiSecret {get;set;}
 public bool HasApiKey {get;set;}
 public bool HasApiSecret {get;set;}
 public string LegacyBaseUrl {get;set;}="";
 public string? SecureKey {get;set;}
 public bool HasSecureKey {get;set;}
}
