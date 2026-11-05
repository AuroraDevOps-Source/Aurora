using Aurora.Contracts;
using System.Text.Json;

static class EquipmentTypeChecks
{
    public static void Run(Action<bool,string> check)
    {
        var supplied=new EquipmentTypeTemplate{Code="28 TDRY",Description="28' Pup Dry Van Trailer",InteriorWidth=8.17m,InteriorHeight=8.25m,ExteriorLength=40,Cubes=819,EmptyWeight=null,Payload=5000,PerMile=2.68m,Axles=2};
        supplied.Validate();var fields=supplied.Fields();
        check(fields["EH_INT_WIDTH"]=="8.17"&&fields["EH_INT_HEIGHT"]=="8.25"&&fields["EH_MAX_CUBE"]=="819"&&fields["EH_LENGTH"]=="40","equipment defaults preserve supplied precision, explicit cube and exterior length");
        check(fields["EH_EMPTY_WEIGHT"] is null&&fields["EH_MAX_WEIGHT"]=="5000"&&fields["EH_TOT_VAR_COST_MI"]=="2.68","equipment profile keeps blank empty weight distinct from payload and per-mile costs");
        var native=supplied.Fields(true);check(native["EH_INT_WIDTH"]=="8.2"&&native["EH_INT_HEIGHT"]=="8.3"&&supplied.InteriorHeight==8.25m,"native dimension precision does not mutate stored equipment defaults");
        var stored=JsonSerializer.Deserialize<EquipmentRoutingProfile>(JsonSerializer.Serialize(new EquipmentRoutingProfile(){EquipmentType=supplied}))!;
        stored.EquipmentType!.RestoreExactDimensions(native);
        check(native["EH_INT_WIDTH"]=="8.17"&&native["EH_INT_HEIGHT"]=="8.25","saved Aurora equipment dimensions reopen exactly");
        native["EH_INT_WIDTH"]="7.5";stored.EquipmentType.RestoreExactDimensions(native);
        check(native["EH_INT_WIDTH"]=="7.5","external Nova dimension edits are not hidden by old profile defaults");
        var changed=supplied.WithFields(native);check(changed.InteriorWidth==7.5m&&supplied.InteriorWidth==8.17m,"per-vehicle overrides do not alter the reusable profile");
        fields["EH_AXLES"]="2.5";try{supplied.WithFields(fields);throw new Exception("Fractional axle accepted");}catch(FormatException){check(true,"fractional axle counts are rejected before saving");}
    }
}
