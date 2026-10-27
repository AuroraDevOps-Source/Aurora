using System.Text.Json;
using Aurora.Contracts;
using System.Text.RegularExpressions;

namespace Aurora.Modules.Routing;

// Structured planning metadata is hidden from dispatcher notes and preserved on edits.
public static class ManifestNotes
{
    private const string TripPrefix="Aurora trip until v1 ";
    private static string TripLine(string? stored)=>WithoutPlanning(stored).StartsWith(TripPrefix,StringComparison.Ordinal)?WithoutPlanning(stored).Split('\n',2)[0].TrimEnd('\r'):"";
    private static string WithoutTrip(string? stored)=>TripLine(stored).Length==0?WithoutPlanning(stored):WithoutPlanning(stored).Split('\n',2).Skip(1).FirstOrDefault()??"";
    public static DateOnly? TripUntil(string? stored)=>TripLine(stored) is {Length:>0} line && DateOnly.TryParseExact(line[TripPrefix.Length..],"yyyy-MM-dd",out var date)?date:null;
    public static string WithTripUntil(string? stored,DateOnly date)=>(PlanningLine(stored).Length>0?PlanningLine(stored)+"\n":"")+TripPrefix+date.ToString("yyyy-MM-dd")+"\n"+WithoutTrip(stored);
    private const string ConsolidationPrefix="Aurora customer consolidation v1 ";
    private static string ConsolidationLine(string? stored)=>WithoutTrip(stored).StartsWith(ConsolidationPrefix,StringComparison.Ordinal)?WithoutTrip(stored).Split('\n',2)[0].TrimEnd('\r'):"";
    private static string WithoutMetadata(string? stored)=>ConsolidationLine(stored).Length==0?WithoutTrip(stored):WithoutTrip(stored).Split('\n',2).Skip(1).FirstOrDefault()??"";
    public static CustomerConsolidation? Consolidation(string? stored)=>ConsolidationLine(stored) is {Length:>0} line?JsonSerializer.Deserialize<CustomerConsolidation>(line[ConsolidationPrefix.Length..]):null;
    public static string WithConsolidation(string? stored,CustomerConsolidation data)
    {
        var state=PlanningLine(stored);var receipt=Receipt(stored);var notes=Read(stored);
        return (state.Length>0?state+"\n":"")+(TripLine(stored).Length>0?TripLine(stored)+"\n":"")+ConsolidationPrefix+JsonSerializer.Serialize(data)+"\n"+(receipt.Length>0?receipt+"\n":"")+notes;
    }
    public const string LockedPrefix="Aurora planning locked v1 ";
    public const string UnlockedPrefix="Aurora planning unlocked v1 ";
    public static bool OptimizationLocked(string? stored)=>(stored??"").StartsWith(LockedPrefix,StringComparison.Ordinal);
    private static string PlanningLine(string? stored) => (stored??"").StartsWith(LockedPrefix,StringComparison.Ordinal) || (stored??"").StartsWith(UnlockedPrefix,StringComparison.Ordinal) ? (stored??"").Split('\n',2)[0].TrimEnd('\r') : "";
    private static string WithoutPlanning(string? stored) => PlanningLine(stored).Length==0 ? stored??"" : (stored??"").Split('\n',2).Skip(1).FirstOrDefault()??"";
    public static string SetPlanningLock(string? stored,bool locked) => (locked?LockedPrefix:UnlockedPrefix)+Guid.NewGuid().ToString("N")+"\n"+WithoutPlanning(stored);
    public static string WithReceipt(string? stored,string receipt)
    {
        var state=PlanningLine(stored);var notes=Read(stored);
        return (state.Length==0?"":state+"\n")+(TripLine(stored).Length>0?TripLine(stored)+"\n":"")+(ConsolidationLine(stored).Length>0?ConsolidationLine(stored)+"\n":"")+receipt+(notes.Length==0?"":"\n"+notes);
    }
    public static string Receipt(string? stored)
    {
        var first = WithoutMetadata(stored).Split('\n', 2)[0].Trim();
        return Regex.IsMatch(first, @"\AAurora (routing|line haul|consolidation) export v1 [A-F0-9]{64}\z") ? first : "";
    }
    public static string Read(string? stored) => Receipt(stored).Length == 0 ? WithoutMetadata(stored) :
        WithoutMetadata(stored).Split('\n', 2).Skip(1).FirstOrDefault()?.TrimStart('\r', '\n') ?? "";
    public static string Write(string? stored, string notes)
    {
        if (notes is null || notes.Length > 4000) throw new FormatException("Notes must be 4,000 characters or fewer.");
        if(PlanningLine(notes).Length>0 || notes.StartsWith(ConsolidationPrefix,StringComparison.Ordinal) || notes.StartsWith(TripPrefix,StringComparison.Ordinal))throw new FormatException("Use Lock from optimization to change planning protection.");
        var receipt = Receipt(stored);
        var state=PlanningLine(stored);
        return (state.Length==0?"":state+"\n")+(TripLine(stored).Length>0?TripLine(stored)+"\n":"")+(ConsolidationLine(stored).Length>0?ConsolidationLine(stored)+"\n":"")+(receipt.Length == 0 ? notes : receipt + (notes.Length == 0 ? "" : "\n" + notes));
    }
}
