namespace Aurora.Contracts;

public sealed record DocumentExtractionSample(string Pro, string Consignee, System.Text.Json.JsonElement Fields);

public sealed record GlobalDocumentSettings(string Instructions, int Revision, bool CanEdit);

public sealed class TenantSettingsDto
{
    public const int MaxGlobalInstructionsLength = 8000;
    public GlobalDocumentSettings? GlobalDocumentSettings { get; set; }
    public string? GlobalInstructionsEdit { get; set; }
    public const int MaxDocumentInstructionsLength = 16000;
    // Older settings clients omit this field. An explicit empty string clears it.
    public string? DocumentExtractionInstructions {get;set;}
    public int Revision {get;set;}
    public decimal DriverCostPerHour {get;set;}
    public string CompanyName {get;set;}="";
    public TenantNavigationSettings? Navigation {get;set;}
    public NovaConnectionSettings Nova {get;set;}=new();
    public FreightOpsConnectionSettings FreightOps {get;set;}=new();
    public DispatchTrackConnectionSettings DispatchTrack {get;set;}=new();
    public ZenventoryConnectionSettings Zenventory {get;set;}=new();
}
public sealed class TenantNavigationSettings
{
    public bool Optimization {get;set;}=true;
    public bool DataEntry {get;set;}=true;
    public bool FreightOps {get;set;}=true;
    public bool Hub {get;set;}=true;
    public bool Nova {get;set;}=true;

    public bool IsVisible(string code)=>code switch
    {
        "aurora"=>Optimization,"freightops"=>FreightOps,"hub"=>Hub,"nova"=>Nova,_=>true
    };
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
