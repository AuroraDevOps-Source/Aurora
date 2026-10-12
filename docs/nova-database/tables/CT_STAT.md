# CT_STAT

Codes and configuration. Status code dictionary shared by order and equipment state fields.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTS_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTS_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | CTS_CODE | VARCHAR(4) | Yes | — | D_STAT_CODE | NONE/NONE |
| 3 | CTS_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | CTS_BGND_COLOR | INTEGER | No | — | RDB$55 | — |
| 5 | CTS_TOO_LONG_TIME | SMALLINT | No | — | RDB$56 | — |
| 6 | CTS_TEXT_COLOR | INTEGER | No | — | RDB$57 | — |
| 7 | CTS_VISIBLE | VARCHAR(1) | No | — | RDB$58 | NONE/NONE |
| 8 | CTS_MFST_VISIBLE | VARCHAR(1) | No | — | RDB$2681 | NONE/NONE |
| 9 | CTS_ORDER_VISIBLE | VARCHAR(1) | No | — | RDB$2682 | NONE/NONE |
| 10 | CTS_DRIVER_CHECK_IN_IND | VARCHAR(1) | No | — | RDB$5005 | NONE/NONE |
| 11 | CTS_REQUIRE_TRACTOR | VARCHAR(1) | No | — | RDB$5351 | NONE/NONE |
| 12 | CTS_REQUIRE_TRAILER | VARCHAR(1) | No | — | RDB$5352 | NONE/NONE |
| 13 | CTS_WEB_DESC | VARCHAR(50) | No | — | RDB$5551 | NONE/NONE |
| 14 | CTS_DISPLAY_ON_WEB_IND | VARCHAR(1) | No | — | RDB$5552 | NONE/NONE |
| 15 | CTS_DOCK_VISIBLE | VARCHAR(1) | No | — | RDB$6436 | NONE/NONE |
| 16 | CTS_EQUIP_AVAIL_SCHED_IND | VARCHAR(1) | No | — | RDB$6516 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTS_CODE_KEY | CTS_CODE | Yes | Yes | ASC | — |
| CTS_PRIM_KEY | CTS_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R056 | CT_STAT_FROM.(CTSF_FROM) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R057 | CT_STAT_FROM.(CTSF_ID) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R098 | EQUIP_AVAIL.(EQA_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R115 | EQUIP_HISTORY.(EH_PRIOR_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R116 | EQUIP_HISTORY.(EH_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R127 | EQUIP_STATUS.(ES_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R192 | OPS_HEADER.(PB_STATUS) | CT_STAT.(CTS_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R197 | OPS_HISTORY.(OPH_PRIOR_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R198 | OPS_HISTORY.(OPH_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R228 | OPS_STOP_REC.(OPS_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`CT_STAT_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
