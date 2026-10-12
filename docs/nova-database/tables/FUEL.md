# FUEL

Equipment and fuel. Fuel records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (FP_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | FP_SEQ | BIGINT | Yes | — | RDB$3353 | — |
| 2 | FP_EQUIP_TYPE | BIGINT | No | — | RDB$3354 | — |
| 3 | FP_EQUIP_NUM | BIGINT | No | — | RDB$3355 | — |
| 4 | FP_DATE | DATE | No | — | RDB$3356 | — |
| 5 | FP_HUB | NUMERIC(10,2) | No | — | RDB$3357 | — |
| 6 | FP_LOCATION | VARCHAR(150) | No | — | RDB$3358 | NONE/NONE |
| 7 | FP_LOC_ST | VARCHAR(2) | No | — | RDB$3359 | NONE/NONE |
| 8 | FP_CARD_TYPE | VARCHAR(8) | No | — | RDB$3360 | NONE/NONE |
| 9 | FP_CARD_NUM | VARCHAR(32) | No | — | RDB$3361 | NONE/NONE |
| 10 | FP_DRIVER | BIGINT | No | — | RDB$3362 | — |
| 11 | FP_TRIP_ID | BIGINT | No | — | RDB$3363 | — |
| 12 | FP_VOUCHER | VARCHAR(32) | No | — | RDB$3364 | NONE/NONE |
| 13 | FP_DRAFT | BIGINT | No | — | RDB$3365 | — |
| 14 | FP_GAL | NUMERIC(11,4) | No | — | RDB$3366 | — |
| 15 | FP_AMT | NUMERIC(13,2) | No | — | RDB$3367 | — |
| 16 | FP_OIL | NUMERIC(8,1) | No | — | RDB$3368 | — |
| 17 | FP_OIL_AMT | NUMERIC(13,2) | No | — | RDB$3369 | — |
| 18 | FP_TIRE_DESC | VARCHAR(128) | No | — | RDB$3370 | NONE/NONE |
| 19 | FP_TIRE_AMT | NUMERIC(13,2) | No | — | RDB$3371 | — |
| 20 | FP_REPAIR_DESC | VARCHAR(255) | No | — | RDB$3372 | NONE/NONE |
| 21 | FP_REPAIR_AMT | NUMERIC(13,2) | No | — | RDB$3373 | — |
| 22 | FP_COMP | VARCHAR(2) | No | — | RDB$3374 | NONE/NONE |
| 23 | FP_ENT_DATE | TIMESTAMP | No | — | RDB$3375 | — |
| 24 | FP_ENT_USER | VARCHAR(12) | No | — | RDB$3376 | NONE/NONE |
| 25 | FP_CHG_DATE | TIMESTAMP | No | — | RDB$3377 | — |
| 26 | FP_CHG_USER | VARCHAR(12) | No | — | RDB$3378 | NONE/NONE |
| 27 | FP_TOLLS | NUMERIC(13,2) | No | — | RDB$3379 | — |
| 28 | FP_FUEL_USE | VARCHAR(12) | No | — | RDB$3380 | NONE/NONE |
| 29 | FP_TICKET | VARCHAR(24) | No | — | RDB$3381 | NONE/NONE |
| 30 | FP_TIME | TIME | No | — | RDB$3382 | — |
| 31 | FP_BULK_IND | VARCHAR(1) | No | — | RDB$3383 | NONE/NONE |
| 32 | FP_ADDITIVE | NUMERIC(8,1) | No | — | RDB$3427 | — |
| 33 | FP_ADDITIVE_AMT | NUMERIC(13,2) | No | — | RDB$3428 | — |
| 34 | FP_REFR | NUMERIC(8,1) | No | — | RDB$3429 | — |
| 35 | FP_REFR_AMT | NUMERIC(13,2) | No | — | RDB$3430 | — |
| 36 | FP_SOURCE | VARCHAR(24) | No | — | RDB$5361 | NONE/NONE |
| 37 | FP_TRAIL1 | VARCHAR(24) | No | — | RDB$7245 | NONE/NONE |
| 38 | FP_TRAIL2 | VARCHAR(24) | No | — | RDB$7246 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| FUEL_DATE_KEY | FP_DATE, FP_EQUIP_TYPE, FP_EQUIP_NUM | No | Yes | ASC | — |
| FUEL_EQUIP_KEY | FP_EQUIP_TYPE, FP_EQUIP_NUM, FP_DATE | No | Yes | ASC | — |
| FUEL_PRIME_KEY | FP_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R129 | FUEL.(FP_CARD_TYPE) | CT_CARD_TYPES.(CTC_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R130 | FUEL.(FP_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R131 | FUEL.(FP_EQUIP_NUM) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R132 | FUEL.(FP_EQUIP_TYPE) | CT_TYPE.(CTT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R271 | TRIPMILES.(TMI_FP_SEQ) | FUEL.(FP_SEQ) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`FUEL_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
