namespace Aurora.Modules.Sync;

// This limits history imported from an external system. It is not a retention
// policy: aging out must never delete an Aurora record or its mapping.
public static class SyncHistoryWindow
{
    public const int Months=3;
    public static DateOnly Cutoff(DateOnly today)=>today.AddMonths(-Months);
    public static bool Includes(string entityType,DateOnly? businessDate,bool open,bool requiredByCurrentWork,DateOnly today)
        => entityType is not ("order" or "manifest") || open || requiredByCurrentWork || businessDate is null || businessDate>=Cutoff(today);
}
