# EQUIP_HISTORY

Equipment and fuel. Equip History records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (EH_ID, EH_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | EH_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | EH_SEQUENCE | BIGINT | Yes | — | RDB$2104 | — |
| 3 | EH_TIME | TIMESTAMP | No | — | RDB$2105 | — |
| 4 | EH_INITIALS | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 5 | EH_STATUS | BIGINT | No | — | D_SYSID | — |
| 6 | EH_STATUS_TIME | TIMESTAMP | No | — | RDB$2106 | — |
| 7 | EH_LOC_TYPE | VARCHAR(1) | No | — | D_LOC_TYPE | NONE/NONE |
| 8 | EH_LOCATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 9 | EH_ASG_DRIVER | BIGINT | No | — | D_SYSID | — |
| 10 | EH_STOP_ID | BIGINT | No | — | D_SYSID | — |
| 11 | EH_ASG_TRACTOR | BIGINT | No | — | D_SYSID | — |
| 12 | EH_ASG_DRIVER2 | BIGINT | No | — | D_SYSID | — |
| 13 | EH_NUM_SHIPMENTS | INTEGER | No | — | RDB$4763 | — |
| 14 | EH_MILES | NUMERIC(9,2) | No | — | RDB$4764 | — |
| 15 | EH_PRIOR_STATUS | BIGINT | No | — | D_SYSID | — |
| 16 | EH_MANIFEST_NUMBER | INTEGER | No | — | RDB$5344 | — |
| 17 | EH_COMMENT | VARCHAR(50) | No | — | RDB$5366 | NONE/NONE |
| 18 | EH_ASG_TRAILER | BIGINT | No | — | RDB$6427 | — |
| 19 | EH_PREV_NUM_SHIPMENTS | INTEGER | No | — | RDB$6832 | — |
| 20 | EH_ODO | NUMERIC(10,2) | No | — | RDB$7124 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| EH_REVERSE_KEY | EH_ID, EH_SEQUENCE | No | Yes | DESC | — |
| EH_SEQUENCE_KEY | EH_SEQUENCE | No | Yes | ASC | — |
| PK_EQUIP_HISTORY | EH_ID, EH_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R109 | EQUIP_HISTORY.(EH_ASG_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R110 | EQUIP_HISTORY.(EH_ASG_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R111 | EQUIP_HISTORY.(EH_ASG_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R112 | EQUIP_HISTORY.(EH_ASG_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R113 | EQUIP_HISTORY.(EH_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R114 | EQUIP_HISTORY.(EH_MANIFEST_NUMBER) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R115 | EQUIP_HISTORY.(EH_PRIOR_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R116 | EQUIP_HISTORY.(EH_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R117 | EQUIP_HISTORY.(EH_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`PREV_NUM_SHIP_B4_INS`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
