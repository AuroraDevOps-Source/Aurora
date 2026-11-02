using System.Globalization;

namespace Aurora.Contracts.Integration;

// Source evidence: OrderEntryDM.pas Fix_lineitem_weights, TOrderLineItem.Cube,
// and EDI204ToOrder.pas. Native cube text is not inherently cubic feet.
public static class LykesNovaMeasurements
{
    // Confirmed Lykes policy. Retain the explicit legacy HandlingUnits helper for v1 callers.
    public static int Pieces(decimal pieces)=>HandlingUnits(pieces,0,"pieces");
    public static decimal CubePerPiece(decimal totalCubicFeet,decimal pieces)
    {
        var count=Pieces(pieces);
        if(totalCubicFeet<0)throw new FormatException("Cube cannot be negative.");
        return totalCubicFeet/count;
    }
    public static decimal Pounds(decimal oplWeight,decimal oplKilos)
    {
        if(oplWeight<0||oplKilos<0)throw new FormatException("Negative freight weight needs review.");
        var converted=oplKilos*2.2046226218487757m;
        if(oplWeight>0&&oplKilos>0&&Math.Abs(oplWeight-converted)>Math.Max(0.02m,oplWeight*0.001m))
            throw new FormatException("Nova pounds and kilograms disagree; reconcile the source row.");
        return oplWeight>0?oplWeight:converted;
    }
    public static decimal CubicFeet(string raw,string? scalarUnit,string? dimensionUnit)
    {
        if(string.IsNullOrWhiteSpace(raw))throw new FormatException("Missing Nova cube needs an explicit value or review.");
        var value=raw.Trim();var cm=value.StartsWith('C')||value.StartsWith('c');
        if(cm){value=value[1..];dimensionUnit="cm";}
        var parts=value.Split(['x','X']);
        decimal Parse(string text)=>decimal.TryParse(text.Trim(),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var n)&&n>=0?n:throw new FormatException("Ambiguous Nova cube text; retain the raw value for review.");
        if(parts.Length==1&&!cm)
        {
            var n=Parse(parts[0]);
            return scalarUnit switch{"ft3"=>n,"m3"=>n*35.31466672148859m,_=>throw new FormatException("Scalar Nova cube requires a confirmed volume unit.")};
        }
        if(parts.Length!=3)throw new FormatException("Two dimensions describe area, not volume; do not create a cube value.");
        var factor=dimensionUnit switch{"in"=>1m/12m,"cm"=>1m/30.48m,"ft"=>1m,"m"=>1m/0.3048m,_=>throw new FormatException("Nova dimensions require a confirmed length unit.")};
        return parts.Aggregate(1m,(volume,part)=>volume*Parse(part)*factor);
    }
    public static int HandlingUnits(decimal pieces,int pallets,string confirmedRule)
    {
        var count=confirmedRule switch{"pieces"=>pieces,"pallets"=>pallets,_=>throw new FormatException("Confirm whether Lykes barcodes identify pieces or pallets.")};
        if(count<1||count>2000||count!=decimal.Truncate(count))throw new FormatException("Handling units must be positive whole counts; preserve commercial pieces separately.");
        return (int)count;
    }
}
