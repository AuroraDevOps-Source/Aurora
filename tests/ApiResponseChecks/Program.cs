using System.Net;
using System.Net.Http.Json;
using Aurora.Client;
using Aurora.Contracts;
using Aurora.Modules.Routing;

async Task Check(HttpStatusCode status,HttpContent content,string expected)
{
    using var response=new HttpResponseMessage(status){Content=content};
    try{await ApiResponse.Check(response);throw new Exception("Expected failure");}
    catch(FormatException ex){if(!ex.Message.Contains(expected))throw new Exception("Unexpected error: "+ex.Message);Console.WriteLine("PASS "+expected);}
}
await Check(HttpStatusCode.InternalServerError,new StringContent("System.NotSupportedException: sensitive stack trace"),"HTTP 500");
await Check(HttpStatusCode.BadGateway,new StringContent("<html>Proxy error</html>"),"HTTP 502");
await Check(HttpStatusCode.BadRequest,new StringContent(""),"HTTP 400");
await Check(HttpStatusCode.Unauthorized,new StringContent(""),"Sign in again");
await Check(HttpStatusCode.Forbidden,new StringContent(""),"Administrator access");
var message=NovaConnectionErrors.UnsupportedPlugin(new NotSupportedException("Not supported plugin 'srp'."));
await Check(HttpStatusCode.BadRequest,JsonContent.Create(new ApiErrorDto(message)),"requires 'Srp'");
await Check(HttpStatusCode.BadRequest,JsonContent.Create(new ApiErrorDto("Check the database path.")),"Check the database path.");
await ApiResponse.Check(new HttpResponseMessage(HttpStatusCode.OK));
Console.WriteLine("All response handling checks passed.");
