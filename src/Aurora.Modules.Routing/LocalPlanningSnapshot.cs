using System.Text.Json.Nodes;
using Aurora.Contracts;

namespace Aurora.Modules.Routing;

public static class LocalPlanningSnapshot
{
    public static string Build(Guid tenantId,Guid sourceId,TerminalDto node,IReadOnlyList<TmsOrder> orders,DateTimeOffset seed)
    {
        var locations=new JsonArray(new JsonObject{["id"]="DEPOT",["latitude"]=node.Latitude,["longitude"]=node.Longitude});
        var pickups=new JsonArray();var deliveries=new JsonArray();var reports=new JsonObject();
        if(orders.Count==0||orders.Select(o=>o.Id).Distinct(StringComparer.Ordinal).Count()!=orders.Count)throw new FormatException("Select distinct saved orders.");
        foreach(var order in orders)
        {
            order.Validate();
            if(order.SourceId!=sourceId)throw new FormatException("Orders must belong to the same workspace source.");
            if(!string.Equals(order.TerminalCode,node.Code,StringComparison.OrdinalIgnoreCase))throw new FormatException("Select orders from one terminal.");
            if(order.Id=="DEPOT")throw new FormatException("DEPOT is reserved for the route origin.");
            var pickup=order.WorkType=="Pickup";
            locations.Add(new JsonObject{["id"]=order.Id,["latitude"]=pickup?order.OriginLatitude:order.Latitude,["longitude"]=pickup?order.OriginLongitude:order.Longitude});
            (pickup?pickups:deliveries).Add(new JsonObject{["id"]=order.Id,[pickup?"pickup":"delivery"]=new JsonObject{["locationId"]=order.Id,["duration"]=order.ServiceSeconds},["properties"]=new JsonObject{["loads"]=new JsonArray(new JsonObject{["dimension"]="weight",["value"]=order.Weight},new JsonObject{["dimension"]="volume",["value"]=order.Cube})}});
            var report=new JsonObject{["shipToName"]=pickup?order.Shipper:order.Customer,["address1"]=pickup?order.OriginAddress:order.Address,["city"]=pickup?order.OriginCity:order.City,["state"]=pickup?order.OriginState:order.State,["postalCode"]=pickup?order.OriginPostalCode:order.PostalCode,["units"]=order.Pieces,["workType"]=order.WorkType,["customerCode"]=order.CustomerCode,["localRevision"]=order.Revision,["sourceId"]=sourceId.ToString(),["specialInstructions"]=order.Notes,["volumeMissing"]=order.Cube<=0};
            if(order.AppointmentFrom is {} from)report["appointment"]=new JsonObject{["blockBegin"]=from.ToString("O"),["blockEnd"]=order.AppointmentTo!.Value.ToString("O"),["confirmed"]=true};
            reports[order.Id]=report;
        }
        return new JsonObject{["settings"]=new JsonObject{["duration"]=10},["locations"]=locations,["depots"]=new JsonArray(new JsonObject{["id"]="DEPOT",["locationId"]="DEPOT"}),["orders"]=new JsonObject{["deliveries"]=deliveries,["pickups"]=pickups},["vehicles"]=new JsonArray(new JsonObject{["id"]="DATE_SEED",["start"]=new JsonObject{["locationId"]="DEPOT",["earliestStartTime"]=seed.ToString("O")}}),["reporting"]=new JsonObject{["auroraTenant"]=tenantId.ToString(),["sourceId"]=sourceId.ToString(),["terminal"]=node.Code,["orders"]=reports}}.ToJsonString();
    }
}
