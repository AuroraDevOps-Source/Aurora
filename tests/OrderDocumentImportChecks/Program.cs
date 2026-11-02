using System.Text.Json;
using Aurora.Api.Features.Routing;
using Aurora.Contracts;

static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
var id=Guid.NewGuid();
var input=JsonSerializer.Serialize(new[]{new TmsOrder {Id="DOC-42",Customer="Consignee",Address="123 Main",Pieces=4,Weight=500,SourceId=id,TerminalId=id,ManifestId=id,CustomerId=id,NativeOrderId=42,Status="Dispatched",Revision=12}});
foreach(var json in new[]{input,JsonSerializer.Serialize(input),JsonSerializer.Serialize("```json\n"+input+"\n```")})
{
 var order=OrderDocumentImport.Parse(json).Single();
 Check(order.Id=="DOC-42" && order.Weight==500 && order.Pieces==4 && order.Address=="123 Main","Business data lost");
 Check(order.SourceId==Guid.Empty && order.TerminalId is null && order.ManifestId is null && order.CustomerId is null && order.NativeOrderId is null && order.Revision==0 && order.Status==WorkspaceOrderStatus.Ready,"Untrusted ownership/lifecycle leaked");
 order.Validate();
}
foreach(var json in new[]{"[]","[null]","{}","not JSON",JsonSerializer.Serialize(Enumerable.Repeat(new TmsOrder(),101))})
{
 try{OrderDocumentImport.Parse(json);throw new Exception("Invalid response accepted: "+json);}
 catch(Exception ex) when(ex is FormatException or JsonException){}
}
Console.WriteLine("Order document conversion checks passed.");
