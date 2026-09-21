using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
var url = "https://user.aurorasoftware.com/.well-known/openid-configuration";
Console.WriteLine(typeof(OpenIdConnectConfiguration).Assembly.FullName);
Console.WriteLine(typeof(SecurityKey).Assembly.FullName);
var configuration = await OpenIdConnectConfigurationRetriever.GetAsync(url, CancellationToken.None);
Console.WriteLine($"Issuer={configuration.Issuer}; JWKS={configuration.JwksUri}; keys={configuration.SigningKeys.Count}");
foreach (var key in configuration.SigningKeys) Console.WriteLine($"Key={key.KeyId}; type={key.GetType().Name}");
