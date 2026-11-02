using System.Text.Json;
using Aurora.Contracts;
using Microsoft.AspNetCore.DataProtection;
namespace Aurora.Api.Features.Auth;

// Stored secret values are protected; public copies expose only configured flags.
public sealed class ConnectionSettingsProtection(IDataProtectionProvider provider,Guid tenantId)
{
 public static string Url(string value,string label,bool required=true)
 {
  if(!required && string.IsNullOrWhiteSpace(value))return "";
  if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0 || uri.AbsoluteUri.Length>2000)
   throw new FormatException($"Enter a valid {label} URL without credentials, query parameters or fragments.");
  return uri.AbsoluteUri;
 }
 string Secret(string? value,string? existing,bool same,string label)
 {
  if(!string.IsNullOrEmpty(value))return provider.CreateProtector("Aurora.TenantConnection.v1",tenantId.ToString(),label).Protect(value);
  if(same && !string.IsNullOrEmpty(existing))return existing;
  throw new FormatException($"Enter {label} for this connection.");
 }
 public string? DispatchTrack(DispatchTrackConnectionSettings input,string? json)
 {
  var old=json is null?null:JsonSerializer.Deserialize<DispatchTrackConnectionSettings>(json);
  if(old is null && new[]{input.ApiUrl,input.ApiCode,input.ApiKey,input.PostOrderUrl,input.UpdateOrderUrl,input.FetchOrderUrl,input.ExportUrl}.All(string.IsNullOrWhiteSpace))return null;
  var value=new DispatchTrackConnectionSettings{ApiUrl=Url(input.ApiUrl,"DispatchTrack API"),PostOrderUrl=Url(input.PostOrderUrl,"post order",false),UpdateOrderUrl=Url(input.UpdateOrderUrl,"update order",false),FetchOrderUrl=Url(input.FetchOrderUrl,"fetch order",false),ExportUrl=Url(input.ExportUrl,"export",false)};
  var same=old?.ApiUrl==value.ApiUrl && old.PostOrderUrl==value.PostOrderUrl && old.UpdateOrderUrl==value.UpdateOrderUrl && old.FetchOrderUrl==value.FetchOrderUrl && old.ExportUrl==value.ExportUrl;
  value.ApiCode=Secret(input.ApiCode,old?.ApiCode,same,"DispatchTrack API code");
  value.ApiKey=Secret(input.ApiKey,old?.ApiKey,same,"DispatchTrack API key");
  return JsonSerializer.Serialize(value);
 }
 public string? Zenventory(ZenventoryConnectionSettings input,string? json)
 {
  var old=json is null?null:JsonSerializer.Deserialize<ZenventoryConnectionSettings>(json);
  if(old is null && new[]{input.BaseUrl,input.ApiKey,input.ApiSecret,input.LegacyBaseUrl,input.SecureKey}.All(string.IsNullOrWhiteSpace))return null;
  var value=new ZenventoryConnectionSettings{BaseUrl=Url(input.BaseUrl,"ZEnventory REST API",false),LegacyBaseUrl=Url(input.LegacyBaseUrl,"ZEnventory legacy API",false)};
  if(value.BaseUrl.Length==0 && value.LegacyBaseUrl.Length==0)throw new FormatException("Enter a ZEnventory REST or legacy API URL.");
  if(value.BaseUrl.Length>0){value.ApiKey=Secret(input.ApiKey,old?.ApiKey,old?.BaseUrl==value.BaseUrl,"ZEnventory API key");value.ApiSecret=Secret(input.ApiSecret,old?.ApiSecret,old?.BaseUrl==value.BaseUrl,"ZEnventory API secret");}
  else if(!string.IsNullOrEmpty(input.ApiKey)||!string.IsNullOrEmpty(input.ApiSecret))throw new FormatException("Enter the ZEnventory REST API URL for these credentials.");
  if(value.LegacyBaseUrl.Length>0)value.SecureKey=Secret(input.SecureKey,old?.SecureKey,old?.LegacyBaseUrl==value.LegacyBaseUrl,"ZEnventory SecureKey");
  else if(!string.IsNullOrEmpty(input.SecureKey))throw new FormatException("Enter the ZEnventory legacy API URL for its SecureKey.");
  return JsonSerializer.Serialize(value);
 }
 public static DispatchTrackConnectionSettings PublicDispatchTrack(string? json)
 {
  var value=json is null?new():JsonSerializer.Deserialize<DispatchTrackConnectionSettings>(json)!;
  value.HasApiCode=!string.IsNullOrEmpty(value.ApiCode);value.HasApiKey=!string.IsNullOrEmpty(value.ApiKey);value.ApiCode=value.ApiKey=null;return value;
 }
 public static ZenventoryConnectionSettings PublicZenventory(string? json)
 {
  var value=json is null?new():JsonSerializer.Deserialize<ZenventoryConnectionSettings>(json)!;
  value.HasApiKey=!string.IsNullOrEmpty(value.ApiKey);value.HasApiSecret=!string.IsNullOrEmpty(value.ApiSecret);value.HasSecureKey=!string.IsNullOrEmpty(value.SecureKey);value.ApiKey=value.ApiSecret=value.SecureKey=null;return value;
 }
}
