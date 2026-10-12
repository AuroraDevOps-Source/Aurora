# DRIV_EXPENSE

Drivers and settlement. Driv Expense records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DX_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DX_SEQ | BIGINT | Yes | — | RDB$3292 | — |
| 2 | DX_DRIV_NUM | BIGINT | No | — | RDB$3293 | — |
| 3 | DX_DATE | DATE | No | — | RDB$3294 | — |
| 4 | DX_ENTER_TIME | TIMESTAMP | No | — | RDB$3295 | — |
| 5 | DX_ENTER_USER | VARCHAR(12) | No | — | RDB$3296 | NONE/NONE |
| 6 | DX_CHG_TIME | TIMESTAMP | No | — | RDB$3297 | — |
| 7 | DX_CHG_USER | VARCHAR(12) | No | — | RDB$3298 | NONE/NONE |
| 8 | DX_PB_ID | BIGINT | No | — | RDB$3299 | — |
| 9 | DX_TRC_REPAIR | NUMERIC(13,2) | No | — | RDB$3300 | — |
| 10 | DX_TLR_REPAIR | NUMERIC(13,2) | No | — | RDB$3301 | — |
| 11 | DX_TIRES | NUMERIC(13,2) | No | — | RDB$3302 | — |
| 12 | DX_TOLLS | NUMERIC(13,2) | No | — | RDB$3303 | — |
| 13 | DX_SCALES | NUMERIC(13,2) | No | — | RDB$3304 | — |
| 14 | DX_LUMPER | NUMERIC(13,2) | No | — | RDB$3305 | — |
| 15 | DX_WASH | NUMERIC(13,2) | No | — | RDB$3306 | — |
| 16 | DX_HOTEL | NUMERIC(13,2) | No | — | RDB$3307 | — |
| 17 | DX_OTHER1 | NUMERIC(13,2) | No | — | RDB$3308 | — |
| 18 | DX_OTHER1_DESC | VARCHAR(512) | No | — | RDB$3309 | NONE/NONE |
| 19 | DX_OTHER2 | NUMERIC(13,2) | No | — | RDB$3310 | — |
| 20 | DX_OTHER2_DESC | VARCHAR(512) | No | — | RDB$3311 | NONE/NONE |
| 21 | DX_CHARGE_DRIV_IND | VARCHAR(1) | No | — | RDB$3312 | NONE/NONE |
| 22 | DX_CHECK_NO | VARCHAR(12) | No | — | RDB$3313 | NONE/NONE |
| 23 | DX_CHECK_AMT | NUMERIC(13,2) | No | — | RDB$3314 | — |
| 24 | DX_CASH_AMT | NUMERIC(13,2) | No | — | RDB$3315 | — |
| 25 | DX_COMMENT | VARCHAR(256) | No | — | RDB$3339 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DRIV_EXPENSE_DATE_KEY | DX_DATE, DX_DRIV_NUM | No | Yes | ASC | — |
| DRIV_EXPENSE_DRV_KEY | DX_DRIV_NUM, DX_DATE | No | Yes | ASC | — |
| DRIV_EXPENSE_KEY | DX_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R079 | DRIV_EXPENSE.(DX_DRIV_NUM) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R080 | DRIV_EXPENSE.(DX_PB_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

`DRIV_EXPENSE_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
