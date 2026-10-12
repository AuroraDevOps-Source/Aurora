# EQUIP_STATUS

Equipment and fuel. Current equipment state; update triggers write EQUIP_HISTORY.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ES_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ES_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | ES_STATUS | BIGINT | No | — | D_SYSID | — |
| 3 | ES_STATUS_DATE | TIMESTAMP | No | — | RDB$184 | — |
| 4 | ES_INITIALS | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 5 | ES_LOC_TYPE | VARCHAR(1) | No | — | D_LOC_TYPE | NONE/NONE |
| 6 | ES_LOCATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 7 | ES_ASG_DRIVER | BIGINT | No | — | D_SYSID | — |
| 8 | ES_ASG_SEQUENCE | INTEGER | No | — | RDB$695 | — |
| 9 | ES_DEST_TYPE | VARCHAR(1) | No | — | D_LOC_TYPE | NONE/NONE |
| 10 | ES_DESTINATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 11 | ES_STOP_ID | BIGINT | No | — | D_SYSID | — |
| 12 | ES_ETA | TIMESTAMP | No | — | RDB$3609 | — |
| 13 | ES_ASG_TRACTOR | BIGINT | No | — | D_SYSID | — |
| 14 | ES_POS_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 15 | ES_POS_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 16 | ES_POS_DISTANCE | DOUBLE PRECISION | No | — | RDB$4074 | — |
| 17 | ES_POS_BEARING | DOUBLE PRECISION | No | — | RDB$4075 | — |
| 18 | ES_POS_DIRECTION | VARCHAR(2) | No | — | RDB$4076 | NONE/NONE |
| 19 | ES_POS_LATITUDE | DOUBLE PRECISION | No | — | RDB$4077 | — |
| 20 | ES_POS_NS | VARCHAR(1) | No | — | RDB$4078 | NONE/NONE |
| 21 | ES_POS_LONGITUDE | DOUBLE PRECISION | No | — | RDB$4079 | — |
| 22 | ES_POS_EW | VARCHAR(1) | No | — | RDB$4080 | NONE/NONE |
| 23 | ES_POS_DATE | TIMESTAMP | No | — | RDB$4081 | — |
| 24 | ES_ASG_DRIVER2 | BIGINT | No | — | D_SYSID | — |
| 25 | ES_NUM_SHIPMENTS | INTEGER | No | — | RDB$4765 | — |
| 26 | ES_MANIFEST_NUMBER | INTEGER | No | — | RDB$5345 | — |
| 27 | ES_COMMENT | VARCHAR(50) | No | — | RDB$5370 | NONE/NONE |
| 28 | ES_ASG_TRAILER | BIGINT | No | — | RDB$6437 | — |
| 29 | ES_ASG_CHASSIS_ID | VARCHAR(50) | No | — | RDB$6810 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| EQUIP_STATUS_PRIMARY | ES_ID | Yes | Yes | ASC | — |
| ES_DRIVER_KEY | ES_ASG_DRIVER, ES_ASG_SEQUENCE, ES_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R120 | EQUIP_STATUS.(ES_ASG_CHASSIS_ID) | EQUIP_HEADER.(EH_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R121 | EQUIP_STATUS.(ES_ASG_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R122 | EQUIP_STATUS.(ES_ASG_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R123 | EQUIP_STATUS.(ES_ASG_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R124 | EQUIP_STATUS.(ES_ASG_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R125 | EQUIP_STATUS.(ES_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R126 | EQUIP_STATUS.(ES_MANIFEST_NUMBER) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R127 | EQUIP_STATUS.(ES_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R128 | EQUIP_STATUS.(ES_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`EQUIP_STATUS_AFTER_INSERT`, `EQUIP_STATUS_AFTER_UPDATE`, `EQUIP_STATUS_BEFORE_UPDATE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
