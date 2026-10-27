using Aurora.Client;
using Aurora.Contracts;

var terminal=Guid.NewGuid();
var source=Guid.NewGuid();
TmsOrder Order(string id)=>new(){Id=id,SourceId=source,TerminalId=terminal,Status=WorkspaceOrderStatus.Ready};
void Check(bool condition,string message){if(!condition)throw new Exception(message);}
void Reject(params TmsOrder[] orders)
{
    try{new OrderPlanningSelection().Set(orders);}
    catch(FormatException){return;}
    throw new Exception("Invalid selection was accepted");
}
var state=new OrderPlanningSelection();
var ten=Enumerable.Range(1,10).Select(i=>Order("FB-"+i)).ToArray();
state.Set(ten);
var selection=state.Take()!;
Check(selection.TerminalId==terminal,"Selected terminal lost");
Check(selection.Orders.ToHashSet().SetEquals(ten.Select(o=>new TmsOrderKey(o.SourceId,o.Id))),"Exact ten-order selection lost");
Check(state.Take() is null,"Selection reused on unrelated navigation");
state.Set([ten[0],ten[0]]);
Check(state.Take()!.Orders.Length==1,"Duplicate order IDs were not removed");
Reject();
var otherTerminal=Order("other");otherTerminal.TerminalId=Guid.NewGuid();Reject(ten[0],otherTerminal);
var unassigned=Order("unassigned");unassigned.TerminalId=null;Reject(unassigned);
var otherSource=Order("FB-1");otherSource.SourceId=Guid.NewGuid();Reject(ten[0],otherSource);
var manifested=Order("manifested");manifested.ManifestId=Guid.NewGuid();Reject(manifested);
var reserved=Order("reserved");reserved.OptimizationDraftId=Guid.NewGuid();Reject(reserved);
var delivered=Order("delivered");delivered.Status="Delivered";Reject(delivered);
Console.WriteLine("Order planning selection checks passed: exact ten orders, terminal, one-time handoff, duplicate IDs, empty/mixed/unavailable selections.");
