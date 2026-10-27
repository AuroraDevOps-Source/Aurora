using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aurora.Contracts;

namespace Aurora.Client;

public static class ApiResponse
{
    public static async Task Check(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode)return;
        if(response.StatusCode==HttpStatusCode.Forbidden)throw new FormatException("Administrator access is required.");
        if(response.StatusCode==HttpStatusCode.Unauthorized)throw new FormatException("Your session has expired. Sign in again.");
        string? error=null;
        RecordErrorLink? link=null;
        try { var body=await response.Content.ReadFromJsonAsync<ApiErrorDto>();error=body?.Error;link=body?.RecordLink; }
        catch(JsonException) { /* Plain text/HTML failures must not mask the original HTTP failure. */ }
        if(string.IsNullOrWhiteSpace(error))error=$"The server could not complete the request (HTTP {(int)response.StatusCode}). Please try again.";
        if(link is not null)throw new RecordErrorException(error,link);
        throw new FormatException(RoutingDisplayText.Clean(error));
    }
}
