using Aurora.Contracts;
using Microsoft.AspNetCore.Components;

namespace Aurora.Client.Pages.Shared;

public partial class NovaRecords
{
    EquipmentTypeTemplate[] _equipmentTypes=[];
    EquipmentTypeTemplate? _equipmentTypeDetails;
    void SelectEquipmentType(ChangeEventArgs e)
    {
        var selected=_equipmentTypes.SingleOrDefault(t=>t.Code==e.Value?.ToString());
        if(selected is null||_busy)return;
        _equipmentTypeDetails=selected.Copy();
        foreach(var pair in selected.Fields())_values["header"][pair.Key]=pair.Value;
    }
    string EquipmentFieldLabel(NovaField field)=>Kind!="fleet"?field.Label:field.Key switch
    {
        "EH_EMPTY_WEIGHT"=>"Empty weight (lb)","EH_MAX_WEIGHT"=>"Payload capacity (lb)","EH_MAX_CUBE"=>"Cubic Planning Capacity (ft³)",
        "EH_TOT_VAR_COST_MI"=>"Cost per mile ($)",
        _=>field.Kind=="dimension"?field.Label+" (ft)":field.Label
    };
}
