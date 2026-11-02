using Shared = Aurora.Contracts.Integration.LykesNovaMeasurements;
namespace Aurora.Integrations.FreightOps;

// Preserve the adapter API; both providers use the same conversion rules.
public static class LykesNovaMeasurements
{
    public static int Pieces(decimal pieces)=>Shared.Pieces(pieces);
    public static decimal CubePerPiece(decimal totalCubicFeet,decimal pieces)=>Shared.CubePerPiece(totalCubicFeet,pieces);
    public static decimal Pounds(decimal oplWeight,decimal oplKilos)=>Shared.Pounds(oplWeight,oplKilos);
    public static decimal CubicFeet(string raw,string? scalarUnit,string? dimensionUnit)=>Shared.CubicFeet(raw,scalarUnit,dimensionUnit);
    public static int HandlingUnits(decimal pieces,int pallets,string confirmedRule)=>Shared.HandlingUnits(pieces,pallets,confirmedRule);
}
