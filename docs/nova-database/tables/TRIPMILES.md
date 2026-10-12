# TRIPMILES

Equipment and fuel. Tripmiles records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TMI_KEYNUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TMI_KEYNUM | BIGINT | Yes | — | RDB$3458 | — |
| 2 | TMI_DATE | DATE | No | — | RDB$3460 | — |
| 3 | TMI_TRIP_NUM | BIGINT | Yes | — | RDB$6945 | — |
| 4 | TMI_POWER_UNIT | BIGINT | No | — | RDB$3461 | — |
| 5 | TMI_DRIV_NUM | BIGINT | No | — | RDB$3459 | — |
| 6 | TMI_HUB | NUMERIC(8,1) | No | — | RDB$3462 | — |
| 7 | TMI_SHIP_NUM | NUMERIC(10,0) | No | — | RDB$3463 | — |
| 8 | TMI_TRAIL1 | VARCHAR(24) | No | — | RDB$3464 | NONE/NONE |
| 9 | TMI_TRAIL2 | VARCHAR(24) | No | — | RDB$3465 | NONE/NONE |
| 10 | TMI_LOAD_EMPTY_IND | VARCHAR(1) | No | — | RDB$3466 | NONE/NONE |
| 11 | TMI_LOCATION | VARCHAR(42) | No | — | RDB$3467 | NONE/NONE |
| 12 | TMI_LOC_TYPE | VARCHAR(1) | No | — | RDB$3468 | NONE/NONE |
| 13 | TMI_LOC_CITY | VARCHAR(32) | No | — | RDB$3474 | NONE/NONE |
| 14 | TMI_LOC_ST | VARCHAR(2) | No | — | RDB$3470 | NONE/NONE |
| 15 | TMI_MILES | NUMERIC(8,1) | No | — | RDB$3471 | — |
| 16 | TMI_TOLL_MI | NUMERIC(8,1) | No | — | RDB$3472 | — |
| 17 | TMI_ROUTE | VARCHAR(12) | No | — | RDB$3473 | NONE/NONE |
| 18 | TMI_FP_SEQ | BIGINT | No | — | RDB$5365 | — |
| 19 | TMI_SOURCE | VARCHAR(12) | No | — | RDB$5801 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| TMI_DATE_KEY | TMI_DATE, TMI_DRIV_NUM | No | Yes | ASC | — |
| TMI_DRIV_KEY | TMI_DRIV_NUM, TMI_DATE, TMI_TRIP_NUM | No | Yes | ASC | — |
| TMI_POWERUNIT_KEY | TMI_POWER_UNIT, TMI_DATE, TMI_TRIP_NUM | No | Yes | ASC | — |
| TMI_STATE_KEY | TMI_LOC_ST, TMI_DATE | No | Yes | ASC | — |
| TRIPMILES_KEY | TMI_KEYNUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R270 | TRIPMILES.(TMI_DRIV_NUM) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R271 | TRIPMILES.(TMI_FP_SEQ) | FUEL.(FP_SEQ) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R272 | TRIPMILES.(TMI_POWER_UNIT) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R273 | TRIPMILES.(TMI_SHIP_NUM) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

`TRIPMILES_BEFORE_TRIGGER`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
