# DRIV_ADVANCE

Drivers and settlement. Driv Advance records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DAV_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DAV_SEQ | BIGINT | Yes | — | RDB$3325 | — |
| 2 | DAV_DRIV_NUM | BIGINT | No | — | RDB$3326 | — |
| 3 | DAV_DATE | DATE | No | — | RDB$3327 | — |
| 4 | DAV_ENTER_TIME | TIMESTAMP | No | — | RDB$3328 | — |
| 5 | DAV_ENTER_USER | VARCHAR(12) | No | — | RDB$3329 | NONE/NONE |
| 6 | DAV_CHG_TIME | TIMESTAMP | No | — | RDB$3330 | — |
| 7 | DAV_CHG_USER | VARCHAR(12) | No | — | RDB$3331 | NONE/NONE |
| 8 | DAV_PB_ID | BIGINT | No | — | RDB$3332 | — |
| 9 | DAV_CASH_AMT | NUMERIC(13,2) | No | — | RDB$3333 | — |
| 10 | DAV_CHECK_AMT | NUMERIC(13,2) | No | — | RDB$3334 | — |
| 11 | DAV_CHECK_NO | VARCHAR(12) | No | — | RDB$3335 | NONE/NONE |
| 12 | DAV_CHECK_DT | DATE | No | — | RDB$3336 | — |
| 13 | DAV_CHARGE_DRIVER_FLAG | SMALLINT | No | — | RDB$3337 | — |
| 14 | DAV_COMMENT | VARCHAR(256) | No | — | RDB$3338 | NONE/NONE |
| 15 | DAV_VOUCHER | VARCHAR(32) | No | — | RDB$3881 | NONE/NONE |
| 16 | DAV_DS_SEQ | BIGINT | No | — | RDB$4211 | — |
| 17 | DAV_SOURCE | VARCHAR(24) | No | — | RDB$5362 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DRIV_ADVANCE_DATE_KEY | DAV_DATE, DAV_DRIV_NUM | No | Yes | ASC | — |
| DRIV_ADVANCE_DRIV_KEY | DAV_DRIV_NUM, DAV_DATE | No | Yes | ASC | — |
| DRIV_ADVANCE_KEY | DAV_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R077 | DRIV_ADVANCE.(DAV_DRIV_NUM) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R078 | DRIV_ADVANCE.(DAV_PB_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

`DRIV_ADVANCE_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
