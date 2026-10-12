# OPS_MFST_INFO

Orders and manifests. Additional manifest timing, mileage, cost and utilization information.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OMI_MFST_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OMI_MFST_ID | BIGINT | Yes | — | RDB$7049 | — |
| 2 | OMI_PRELOAD | VARCHAR(1) | No | — | RDB$7050 | NONE/NONE |
| 3 | OMI_START_TIME | TIMESTAMP | No | — | RDB$7051 | — |
| 4 | OMI_END_TIME | TIMESTAMP | No | — | RDB$7052 | — |
| 6 | OMI_DEPART_TIME | TIMESTAMP | No | — | RDB$7054 | — |
| 7 | OMI_DRIV_TIME | INTEGER | No | — | RDB$7055 | — |
| 8 | OMI_TRIP_MILES | INTEGER | No | — | RDB$7056 | — |
| 9 | OMI_REVENUE | NUMERIC(16,2) | No | — | RDB$7057 | — |
| 10 | OMI_SERVICE_TIME | INTEGER | No | — | RDB$7058 | — |
| 11 | OMI_TRIP_DURATION | INTEGER | No | — | RDB$7059 | — |
| 12 | OMI_UNIT_UTILIZATION | NUMERIC(15,4) | No | — | RDB$7060 | — |
| 13 | OMI_TRIP_COST | NUMERIC(16,2) | No | — | RDB$7061 | — |
| 14 | OMI_RETURN_ETA | TIMESTAMP | No | — | RDB$7062 | — |
| 15 | OMI_CREATE_DT | TIMESTAMP | No | — | RDB$7063 | — |
| 16 | OMI_INITIALS | VARCHAR(12) | No | — | RDB$7064 | NONE/NONE |
| 17 | OMI_LOAD_TIME | INTEGER | No | — | RDB$7065 | — |
| 18 | OMI_SERVICE_TIME_STD | INTEGER | No | — | RDB$7067 | — |
| 19 | OMI_DEPART_TIME_ACTUAL | TIMESTAMP | No | — | RDB$7128 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_OPS_MFST_INFO | OMI_MFST_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R208 | OPS_MFST_INFO.(OMI_MFST_ID) | OPS_HEADER.(PB_ID) | High | Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

`OPS_MFST_INFO_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
