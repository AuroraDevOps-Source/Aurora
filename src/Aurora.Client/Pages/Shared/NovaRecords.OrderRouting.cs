using System.Net.Http.Json;
using Aurora.Contracts;

namespace Aurora.Client.Pages.Shared;

public partial class NovaRecords
{
    bool _lineHaul, _routingLoaded;
    string _receivingTerminal = "";
    NovaOrderRouting _orderRouting = new(null, 0);
    TerminalDto[] _routingTerminals = [];
    long[] _bulkOrderIds = [];

    async Task LoadOrderRouting()
    {
        _routingLoaded=false;_lineHaul=false;_receivingTerminal="";_orderRouting=new(null,0);
        if(Kind!="orders")return;
        using var nodes=await Http.GetAsync("api/v1/aurora/firebird/terminals");
        await Check(nodes);_routingTerminals=await nodes.Content.ReadFromJsonAsync<TerminalDto[]>() ?? [];
        if(!_new)
        {
            using var routing=await Http.GetAsync($"api/v1/aurora/firebird/orders/{_record!.Id}/routing");
            await Check(routing);_orderRouting=(await routing.Content.ReadFromJsonAsync<NovaOrderRouting>())!;
        }
        _lineHaul=_orderRouting.ReceivingTerminal is not null;
        _receivingTerminal=_orderRouting.ReceivingTerminal ?? "";
        _routingLoaded=true;
    }
    void OpenBulkConsignee()=>_bulkOrderIds=_selectedOrders.Order().ToArray();
    async Task CloseBulkConsignee()
    {
        _bulkOrderIds=[];ClearOrderSelection();await Load();
    }
}
