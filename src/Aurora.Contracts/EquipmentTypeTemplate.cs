using System.Globalization;

namespace Aurora.Contracts;

// Reusable equipment defaults. Measurements are feet, pounds and cubic feet;
// costs are dollars per mile. Supplied cube is independent of L x W x H.
public sealed class EquipmentTypeTemplate
{
    public string Code {get;set;}="";
    public string Description {get;set;}="";
    public decimal? PerMile {get;set;}
    public decimal? InteriorLength {get;set;}
    public decimal? InteriorWidth {get;set;}
    public decimal? InteriorHeight {get;set;}
    public decimal? Cubes {get;set;}
    public decimal? ExteriorLength {get;set;}
    public decimal? ExteriorWidth {get;set;}
    public decimal? ExteriorHeight {get;set;}
    public decimal? LengthWithPower {get;set;}
    public string VehicleClass {get;set;}="";
    public decimal? EmptyWeight {get;set;}
    public decimal? Payload {get;set;}
    public int? Axles {get;set;}
    public string FreightType {get;set;}="";
    public string LoadingAccess {get;set;}="";
    public EquipmentTypeTemplate Copy()=>(EquipmentTypeTemplate)MemberwiseClone();
    public static readonly string[] DimensionFields=["EH_LENGTH","EH_WIDTH","EH_HEIGHT","EH_INT_LENGTH","EH_INT_WIDTH","EH_INT_HEIGHT"];
    public Dictionary<string,string?> Fields(bool novaPrecision=false)
    {
        string? F(decimal? value)=>value?.ToString(CultureInfo.InvariantCulture);
        var result=new Dictionary<string,string?>{
            ["EH_TOT_VAR_COST_MI"]=F(PerMile),["EH_INT_LENGTH"]=F(InteriorLength),["EH_INT_WIDTH"]=F(InteriorWidth),["EH_INT_HEIGHT"]=F(InteriorHeight),
            ["EH_MAX_CUBE"]=F(Cubes),["EH_LENGTH"]=F(ExteriorLength),["EH_WIDTH"]=F(ExteriorWidth),["EH_HEIGHT"]=F(ExteriorHeight),
            ["EH_EMPTY_WEIGHT"]=F(EmptyWeight),["EH_MAX_WEIGHT"]=F(Payload),["EH_AXLES"]=Axles?.ToString(CultureInfo.InvariantCulture)};
        if(novaPrecision)foreach(var key in DimensionFields)if(result[key] is {} value)result[key]=decimal.Round(decimal.Parse(value,CultureInfo.InvariantCulture),1,MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        return result;
    }
    public EquipmentTypeTemplate WithFields(IReadOnlyDictionary<string,string?> fields)
    {
        decimal? N(string key)=>string.IsNullOrWhiteSpace(fields.GetValueOrDefault(key))?null:decimal.Parse(fields[key]!,CultureInfo.InvariantCulture);
        var copy=Copy();copy.PerMile=N("EH_TOT_VAR_COST_MI");copy.InteriorLength=N("EH_INT_LENGTH");copy.InteriorWidth=N("EH_INT_WIDTH");copy.InteriorHeight=N("EH_INT_HEIGHT");
        copy.Cubes=N("EH_MAX_CUBE");copy.ExteriorLength=N("EH_LENGTH");copy.ExteriorWidth=N("EH_WIDTH");copy.ExteriorHeight=N("EH_HEIGHT");
        copy.EmptyWeight=N("EH_EMPTY_WEIGHT");copy.Payload=N("EH_MAX_WEIGHT");var axles=N("EH_AXLES");
        if(axles is {} a&&(a!=decimal.Truncate(a)||a>int.MaxValue))throw new FormatException("Axles must be a whole number.");
        copy.Axles=axles is null?null:(int)axles;copy.Validate();return copy;
    }
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Code)||Code.Length>80||string.IsNullOrWhiteSpace(Description)||Description.Length>200)throw new FormatException("Select a valid equipment type profile.");
        if(new[]{PerMile,InteriorLength,InteriorWidth,InteriorHeight,Cubes,ExteriorLength,ExteriorWidth,ExteriorHeight,LengthWithPower,EmptyWeight,Payload}.Any(n=>n is <0 or >99999999))throw new FormatException("Equipment dimensions, capacities and costs must be nonnegative and within supported limits.");
        if(Axles is <1||Cubes is {} cube&&cube!=decimal.Truncate(cube))throw new FormatException("Axles must be positive and cubic capacity must be a whole number.");
        if(new[]{VehicleClass,FreightType,LoadingAccess}.Any(v=>v is null||v.Length>200))throw new FormatException("Equipment details must be at most 200 characters.");
    }
    public void RestoreExactDimensions(Dictionary<string,string?> fields)
    {
        var exact=Fields();var native=Fields(true);
        // Never mask a dimension subsequently changed directly in Nova.
        foreach(var key in DimensionFields)if(SameNumber(fields.GetValueOrDefault(key),native[key]))fields[key]=exact[key];
    }
    public static bool SameNumber(string? a,string? b)=>string.IsNullOrWhiteSpace(a)&&string.IsNullOrWhiteSpace(b)||
        decimal.TryParse(a,NumberStyles.Number,CultureInfo.InvariantCulture,out var x)&&decimal.TryParse(b,NumberStyles.Number,CultureInfo.InvariantCulture,out var y)&&x==y;
}
