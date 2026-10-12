# OPS_HISTORY

Orders and manifests. Order and stop event history, intentionally retained when a header is deleted.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OPH_ORDER_ID, OPH_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OPH_ORDER_ID | NUMERIC(9,0) | Yes | — | RDB$703 | — |
| 2 | OPH_SEQUENCE | BIGINT | Yes | — | RDB$704 | — |
| 3 | OPH_STOP_ID | BIGINT | Yes | — | D_SYSID | — |
| 4 | OPH_TIME | TIMESTAMP | Yes | — | RDB$705 | — |
| 5 | OPH_INITIALS | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 6 | OPH_STATUS | BIGINT | No | — | D_SYSID | — |
| 7 | OPH_STATUS_TIME | TIMESTAMP | No | — | RDB$706 | — |
| 8 | OPH_LOC_TYPE | VARCHAR(1) | No | — | D_LOC_TYPE | NONE/NONE |
| 9 | OPH_LOCATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 10 | OPH_APPT_TIME | TIMESTAMP | No | — | RDB$3724 | — |
| 11 | OPH_ARRIVE_TIME | TIMESTAMP | No | — | RDB$707 | — |
| 12 | OPH_DEPART_TIME | TIMESTAMP | No | — | RDB$708 | — |
| 13 | OPH_BACKIN_TIME | TIMESTAMP | No | — | RDB$709 | — |
| 14 | OPH_RELEASE_TIME | TIMESTAMP | No | — | RDB$710 | — |
| 15 | OPH_DISPATCH_TIME | TIMESTAMP | No | — | RDB$711 | — |
| 16 | OPH_ONBOARD_TIME | TIMESTAMP | No | — | RDB$712 | — |
| 17 | OPH_DELIVERED_TIME | TIMESTAMP | No | — | RDB$713 | — |
| 18 | OPH_RECOVERED_TIME | TIMESTAMP | No | — | RDB$714 | — |
| 19 | OPH_OVER | NUMERIC(15,3) | No | — | D_PIECES | — |
| 20 | OPH_SHORT | NUMERIC(15,3) | No | — | D_PIECES | — |
| 21 | OPH_DAMAGED | NUMERIC(15,3) | No | — | D_PIECES | — |
| 22 | OPH_DRIVER1 | BIGINT | No | — | D_SYSID | — |
| 23 | OPH_DRIVER2 | BIGINT | No | — | D_SYSID | — |
| 24 | OPH_TRACTOR | BIGINT | No | — | D_SYSID | — |
| 25 | OPH_TRAILER | BIGINT | No | — | D_SYSID | — |
| 26 | OPH_REASON | VARCHAR(200) | No | — | OPH_REASON | NONE/NONE |
| 27 | OPH_EDI_REASON | VARCHAR(4) | No | — | RDB$3686 | NONE/NONE |
| 28 | OPH_STATUS_COMMENT | VARCHAR(250) | No | — | RDB$3687 | NONE/NONE |
| 29 | OPH_204_ACCEPTED | VARCHAR(1) | No | — | RDB$3688 | NONE/NONE |
| 30 | OPH_PRIOR_STATUS | BIGINT | No | — | D_SYSID | — |
| 31 | OPH_PRIOR_APPT_TIME | TIMESTAMP | No | — | RDB$3725 | — |
| 32 | OPH_PRIOR_ARRIVE_TIME | TIMESTAMP | No | — | RDB$3726 | — |
| 33 | OPH_PRIOR_DEPART_TIME | TIMESTAMP | No | — | RDB$3727 | — |
| 34 | OPH_PRIOR_BACKIN_TIME | TIMESTAMP | No | — | RDB$3728 | — |
| 35 | OPH_PRIOR_RELEASE_TIME | TIMESTAMP | No | — | RDB$3729 | — |
| 36 | OPH_MANIFEST_NUMBER | INTEGER | No | — | RDB$4263 | — |
| 37 | OPH_MANIFESTED_TIME | TIMESTAMP | No | — | RDB$4264 | — |
| 38 | OPH_PRIOR_STATUS_TIME | TIMESTAMP | No | — | RDB$4976 | — |
| 39 | OPH_PRIOR_EDI_REASON | VARCHAR(4) | No | — | RDB$4989 | NONE/NONE |
| 40 | OPH_PRIOR_ARRIVE_REASON | VARCHAR(4) | No | — | RDB$4990 | NONE/NONE |
| 41 | OPH_PRIOR_BACKIN_REASON | VARCHAR(4) | No | — | RDB$4991 | NONE/NONE |
| 42 | OPH_PRIOR_RELEASE_REASON | VARCHAR(4) | No | — | RDB$4992 | NONE/NONE |
| 43 | OPH_PRIOR_DEPART_REASON | VARCHAR(4) | No | — | RDB$4993 | NONE/NONE |
| 44 | OPH_PRIOR_APPT_REASON | VARCHAR(4) | No | — | RDB$4994 | NONE/NONE |
| 45 | OPH_ARRIVE_REASON | VARCHAR(4) | No | — | RDB$4995 | NONE/NONE |
| 46 | OPH_BACKIN_REASON | VARCHAR(4) | No | — | RDB$4996 | NONE/NONE |
| 47 | OPH_RELEASE_REASON | VARCHAR(4) | No | — | RDB$4997 | NONE/NONE |
| 48 | OPH_DEPART_REASON | VARCHAR(4) | No | — | RDB$4998 | NONE/NONE |
| 49 | OPH_APPT_REASON | VARCHAR(4) | No | — | RDB$4999 | NONE/NONE |
| 50 | OPH_PRIOR_OVER | NUMERIC(15,3) | No | — | D_PIECES | — |
| 51 | OPH_PRIOR_SHORT | NUMERIC(15,3) | No | — | D_PIECES | — |
| 52 | OPH_PRIOR_DAMAGED | NUMERIC(15,3) | No | — | D_PIECES | — |
| 53 | OPH_PRIOR_ONBOARD_TIME | TIMESTAMP | No | — | RDB$5537 | — |
| 54 | OPH_PRIOR_DELIVERED_TIME | TIMESTAMP | No | — | RDB$5538 | — |
| 55 | OPH_NOTIFICATION_PROCESSED | TIMESTAMP | No | — | RDB$7185 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| OH_REVERSE_KEY | OPH_ORDER_ID, OPH_SEQUENCE | No | Yes | DESC | — |
| OH_TIME_KEY | OPH_TIME, OPH_SEQUENCE | No | Yes | ASC | — |
| PK_OPS_HISTORY | OPH_ORDER_ID, OPH_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R193 | OPS_HISTORY.(OPH_DRIVER1) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R194 | OPS_HISTORY.(OPH_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R195 | OPS_HISTORY.(OPH_MANIFEST_NUMBER) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R196 | OPS_HISTORY.(OPH_ORDER_ID) | OPS_HEADER.(PB_ID) | High | OPS_HEADER_AFTER_INSERT and OPS_HEADER_AFTER_UPDATE insert NEW.PB_ID into OPH_ORDER_ID OPS_HEADER_AFTER_DELETE deliberately retains history; a missing header can be intentional. |
| R197 | OPS_HISTORY.(OPH_PRIOR_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R198 | OPS_HISTORY.(OPH_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R199 | OPS_HISTORY.(OPH_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | OPS_STOP_REC_BEFORE_UPDATE writes NEW.OPS_STOP_ID into OPH_STOP_ID Header-level history uses OPH_STOP_ID=0; do not classify zero as an orphan. |
| R200 | OPS_HISTORY.(OPH_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R201 | OPS_HISTORY.(OPH_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`OPS_HISTORY_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
