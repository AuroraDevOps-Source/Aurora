# DRIVER_PKUP_DELV

Drivers and settlement. Driver/equipment assignment list linking loads and stops.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DPD_ID, DPD_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DPD_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | DPD_TRAILER_ID | BIGINT | No | — | D_SYSID | — |
| 3 | DPD_SEQUENCE | INTEGER | Yes | — | RDB$692 | — |
| 4 | DPD_LOAD_NUMBER | INTEGER | No | — | RDB$693 | — |
| 5 | DPD_STOP_ID | BIGINT | No | — | D_SYSID | — |
| 6 | DPD_ACTIVE_PLAN | VARCHAR(1) | No | — | RDB$3743 | NONE/NONE |
| 7 | DPD_DATE | TIMESTAMP | No | — | RDB$3744 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DPD_LOAD_KEY | DPD_LOAD_NUMBER, DPD_ID | No | Yes | ASC | — |
| DPD_STOP_KEY | DPD_STOP_ID, DPD_ID | No | Yes | ASC | — |
| DPD_TRAILER_KEY | DPD_TRAILER_ID, DPD_SEQUENCE | No | Yes | ASC | — |
| PK_DRIVER_PKUP_DELV | DPD_ID, DPD_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R070 | DRIVER_PKUP_DELV.(DPD_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R071 | DRIVER_PKUP_DELV.(DPD_LOAD_NUMBER) | OPS_HEADER.(PB_ID) | High | Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R072 | DRIVER_PKUP_DELV.(DPD_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | Trigger OPS_STOP_REC_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R073 | DRIVER_PKUP_DELV.(DPD_TRAILER_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

`DPD_AFTER_DELETE`, `DPD_BEFORE_INSERT`, `DPD_BEFORE_UPDATE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
