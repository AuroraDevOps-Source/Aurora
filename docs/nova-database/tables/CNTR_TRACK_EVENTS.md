# CNTR_TRACK_EVENTS

Containers and warehouse. Cntr Track Events records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTE_BOL, CTE_CNTR_ID, CTE_TIME, CTE_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTE_BOL | VARCHAR(80) | Yes | — | D_CONTROL_NUMBER | NONE/NONE |
| 2 | CTE_CNTR_ID | VARCHAR(14) | Yes | — | D_UNIT_ID | NONE/NONE |
| 3 | CTE_TIME | TIMESTAMP | Yes | — | RDB$6185 | — |
| 4 | CTE_SEQUENCE | INTEGER | Yes | — | RDB$6186 | — |
| 5 | CTE_EVENT_CODE | VARCHAR(2) | No | — | RDB$6187 | NONE/NONE |
| 6 | CTE_EVENT_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 7 | CTE_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 8 | CTE_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 9 | CTE_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 10 | CTE_LATITUE | DOUBLE PRECISION | No | — | RDB$6188 | — |
| 11 | CTE_LONGITUDE | DOUBLE PRECISION | No | — | RDB$6189 | — |
| 12 | CTE_EVENT_LOC_CODE | VARCHAR(10) | No | — | RDB$6190 | NONE/NONE |
| 13 | CTE_RAIL_CAR | VARCHAR(25) | No | — | RDB$6191 | NONE/NONE |
| 14 | CTE_VESSEL_NAME | VARCHAR(25) | No | — | RDB$6192 | NONE/NONE |
| 15 | CTE_VOYAGE_NUMBER | VARCHAR(25) | No | — | RDB$6193 | NONE/NONE |
| 16 | CTE_FACILITY | VARCHAR(25) | No | — | RDB$6194 | NONE/NONE |
| 17 | CTE_FACILITY_CODE | VARCHAR(25) | No | — | RDB$6195 | NONE/NONE |
| 18 | CTE_MODE | VARCHAR(25) | No | — | RDB$6196 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CNTR_TRACK_EVENTS | CTE_BOL, CTE_CNTR_ID, CTE_TIME, CTE_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R042 | CNTR_TRACK_EVENTS.(CTE_BOL, CTE_CNTR_ID) | CNTR_TRACK_CONTAINER.(CTC_BOL, CTC_CNTR_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
