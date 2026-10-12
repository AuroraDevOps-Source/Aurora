# Uncertainties and relationship caveats

This is a table/relationship map, not a data-integrity audit. **No orphan records have been claimed or counted.** User-table contents were not queried.

| Question | Evidence / why it matters |
| --- | --- |
| Which relationships are maintained by Nova application code? | There are zero declared FKs. Trigger cleanup and Aurora joins establish selected mappings, but Nova client/service source was not audited. |
| Are all driver fields internal equipment IDs? | Aurora explicitly joins PB_CURRENT_DRIVER to EQUIP_HEADER.EH_ID. DRIVER_HEADER and multiple driver history/assignment tables also exist. Legacy driver numbers and subtype restrictions need confirmation. |
| Which location table applies to each location value? | OPS_LOC_TYPE/OPS_LOCATION, OPS_MFST_LOC_TYPE/OPS_MFST_LOCATION, PB_STATUS_LOC_TYPE/PB_STATUS_LOCATION and ES_LOC_TYPE/ES_LOCATION form discriminated references. They are not unconditional LOCMAST links. |
| Are references to deleted headers legitimate history? | OPS_HEADER_AFTER_DELETE explicitly preserves OPS_HISTORY for EDI delete/recreate. Header-level history stores OPH_STOP_ID=0. A naïve anti-join would produce false alarms. |
| Who cleans up the order-stop side of a manifest bridge? | OPS_STOP_REC_AFTER_DELETE removes links where OSM_STOP_ID equals the deleted stop. It does not explicitly remove links where OSM_PKUP_DELV_ID equals it. Application behavior is unknown. |
| Is the bridge authoritative, or OPS_MANIFEST_NUMBER? | Aurora writes both and reads the bridge for manifest membership. Their consistency across all Nova operations was not checked. |
| Can an order stop appear on more than one manifest? | OPS_MANIFEST_STOPS has a primary key on OSM_STOP_ID + OSM_SEQUENCE, not a uniqueness guarantee on OSM_PKUP_DELV_ID. Aurora checks prior membership, but other clients may differ. |
| What do zero, blank, NULL and special IDs mean? | Aurora treats NULL/zero manifest numbers as unassigned; trigger history uses stop 0. Other tables' sentinel rules are unverified. |
| What is the complete tariff and contract key? | Effective dates, ranges and record-number indirection appear throughout pricing. Matching a tariff ID alone can multiply records. Composite candidates are documented where clear; temporal semantics remain unverified. |
| How do legacy PRO references join to orders? | Many records carry location/PRO number/suffix or shipment numbers. The map does not assume these equal OPS_HEADER.PB_ID. |
| Which tables own accounting/vendor IDs? | Company, vendor and general-ledger references may resolve in a separate database or subsystem. No invented external tables were added. |
| What entity owns an insurance record? | CA_INSURANCE has CAI_OWNER_TYPE plus CAI_OWNER_ID; do not join all owners to ACCOUNT without interpreting owner type. |
| Are agent contracts owned or shared? | ICAC_CONTRACT_ID plausibly links to ICA_CONTRACT_ID, but the latter is not unique. This is explicitly Low confidence. |
| What is the intended warehouse hierarchy? | CT_WHSE, CT_WHSE_LOCS and CT_WHSE_UNITS have unique indexes rather than declared primary keys. The CTWHU_LOC candidate is documented, but the whole hierarchy is not established. |
| Is webhook delivery active elsewhere? | Both TRG_OPS_HEADER_WH_AI and TRG_OPS_HEADER_WH_AU are inactive. Their source establishes the order reference, not active enqueue behavior. |

Seven tables have no declared primary key: `AC_RPT_SHIPMENTS`, `CT_REGION_RANGES`, `CT_REGION_USER_DEFS`, `CT_WHSE`, `CT_WHSE_LOCS`, `CT_WHSE_UNITS`, and `GOMOTIVELANDING`. Several have unique indexes; the individual catalogs distinguish those from declared constraints. An index named `PK_AC_RPT_SHIPMENTS` is not itself a declared primary-key constraint.

Every relationship catalog entry records exact endpoints, evidence, target uniqueness and type compatibility. Storage differences can be harmless integer widening or CHAR/VARCHAR differences, but they still require review; a Low label is not a finding of corruption. High-confidence trigger mappings keep their High label even where physical types differ.

Tables with no established links are shown in the HTML under Evidence & open questions. This means no link was established by the mapping rules and inspected evidence; it does not prove independence, emptiness, disuse or obsolescence.
