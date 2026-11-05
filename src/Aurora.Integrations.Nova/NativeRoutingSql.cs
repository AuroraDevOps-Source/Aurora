namespace Aurora.Integrations.Nova;

// Native location is physical custody, not PB_LOCATION (order ownership),
// OPS_DELIVERY_TERMINAL (service terminal), or PB_VIA_1/2 (planned path).
internal static class NativeRoutingSql
{
    internal static string Status(string h = "h", string s = "s") => $"COALESCE(NULLIF({s}.OPS_STATUS,0),{h}.PB_STATUS)";
    internal static string Terminal(string h = "h", string s = "s") => $"""
        CASE WHEN {s}.OPS_TYPE='P' THEN TRIM({h}.PB_LOCATION)
        WHEN NULLIF(TRIM({s}.OPS_LOC_TYPE),'') IS NOT NULL THEN
            CASE WHEN {s}.OPS_LOC_TYPE='L' THEN TRIM({s}.OPS_LOCATION) ELSE '' END
        WHEN NULLIF(TRIM({h}.PB_STATUS_LOC_TYPE),'') IS NOT NULL THEN
            CASE WHEN {h}.PB_STATUS_LOC_TYPE='L' THEN TRIM({h}.PB_STATUS_LOCATION) ELSE '' END
        ELSE TRIM({h}.PB_LOCATION) END
        """;
    internal static string Dock(string h = "h", string s = "s") => $"""
        ({s}.OPS_TYPE='D' AND ({s}.OPS_LOC_TYPE='L' OR (NULLIF(TRIM({s}.OPS_LOC_TYPE),'') IS NULL AND {h}.PB_STATUS_LOC_TYPE='L')) AND EXISTS(SELECT 1 FROM CT_STAT ds WHERE ds.CTS_ID={Status(h,s)} AND TRIM(ds.CTS_CODE)='DOCK')
         AND EXISTS(SELECT 1 FROM LOCMAST dl WHERE dl.LM_ID=({Terminal(h,s)})))
        """;
    // Retained links are history only after completion AND explicit dock receipt.
    // Missing manifests/statuses remain blocked rather than silently releasing freight.
    internal static string Unassigned(string h = "h", string s = "s") => $"""
        ((COALESCE({s}.OPS_MANIFEST_NUMBER,0)=0 OR ({Dock(h,s)} AND EXISTS(
            SELECT 1 FROM OPS_HEADER pm JOIN CT_STAT ps ON ps.CTS_ID=pm.PB_STATUS
            WHERE pm.PB_ID={s}.OPS_MANIFEST_NUMBER AND pm.PB_TYPE='M' AND TRIM(ps.CTS_CODE)='CPLT')))
         AND NOT EXISTS(SELECT 1 FROM OPS_MANIFEST_STOPS ml
            LEFT JOIN OPS_STOP_REC mr ON mr.OPS_STOP_ID=ml.OSM_STOP_ID
            LEFT JOIN OPS_HEADER mh ON mh.PB_ID=mr.OPS_ORDER_ID AND mh.PB_TYPE='M'
            LEFT JOIN CT_STAT ms ON ms.CTS_ID=mh.PB_STATUS
            WHERE ml.OSM_PKUP_DELV_ID={s}.OPS_STOP_ID AND
                (COALESCE(TRIM(ms.CTS_CODE),'')<>'CPLT' OR NOT {Dock(h,s)})))
        """;
    internal static string Ready(string h = "h", string s = "s") => $"""
        ({h}.PB_TYPE='O' AND {h}.PB_DT_CANC IS NULL
         AND {s}.OPS_DELIVERED_TIME IS NULL AND {s}.OPS_TO_AGENT_TIME IS NULL
         AND (({s}.OPS_TYPE='D' AND ({Status(h,s)} IN @readyStatuses OR {Dock(h,s)}))
           OR ({s}.OPS_TYPE='P' AND {Status(h,s)} IN @pickupStatuses AND COALESCE({s}.OPS_PICKED_UP,'N')<>'Y'))
         AND {Unassigned(h,s)})
        """;
}
