# TARIFF_POI_RATES

Pricing and tariffs. Tariff Poi Rates records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TPR_TARIFF_ID, TPR_EFF_DT, TPR_ACCOUNT, TPR_TYPE, TPR_KEYWORD, TPR_LOW_ZIP, TPR_HI_ZIP, TPR_STATE_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TPR_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$6332 | NONE/NONE |
| 2 | TPR_EFF_DT | DATE | Yes | — | RDB$6333 | — |
| 3 | TPR_ACCOUNT | VARCHAR(12) | Yes | — | RDB$6334 | NONE/NONE |
| 4 | TPR_TYPE | VARCHAR(2) | Yes | — | RDB$6335 | NONE/NONE |
| 5 | TPR_KEYWORD | VARCHAR(8) | Yes | — | RDB$6336 | NONE/NONE |
| 6 | TPR_LOW_ZIP | VARCHAR(6) | Yes | — | RDB$6337 | NONE/NONE |
| 7 | TPR_HI_ZIP | VARCHAR(6) | Yes | — | RDB$6338 | NONE/NONE |
| 8 | TPR_STATE_ID | VARCHAR(2) | Yes | — | RDB$6339 | NONE/NONE |
| 9 | TPR_MIN | NUMERIC(18,2) | No | — | RDB$6340 | — |
| 10 | TPR_MAX | NUMERIC(18,2) | No | — | RDB$6341 | — |
| 11 | TPR_INV_LOW_1 | NUMERIC(18,2) | No | — | RDB$6342 | — |
| 12 | TPR_INV_HI_1 | NUMERIC(18,2) | No | — | RDB$6343 | — |
| 13 | TPR_PCT_1 | NUMERIC(18,4) | No | — | RDB$6344 | — |
| 14 | TPR_INV_LOW_2 | NUMERIC(18,2) | No | — | RDB$6345 | — |
| 15 | TPR_INV_HI_2 | NUMERIC(18,2) | No | — | RDB$6346 | — |
| 16 | TPR_PCT_2 | NUMERIC(18,4) | No | — | RDB$6347 | — |
| 17 | TPR_INV_LOW_3 | NUMERIC(18,2) | No | — | RDB$6348 | — |
| 18 | TPR_INV_HI_3 | NUMERIC(18,2) | No | — | RDB$6349 | — |
| 19 | TPR_PCT_3 | NUMERIC(18,4) | No | — | RDB$6350 | — |
| 20 | TPR_INV_LOW_4 | NUMERIC(18,2) | No | — | RDB$6351 | — |
| 21 | TPR_INV_HI_4 | NUMERIC(18,2) | No | — | RDB$6352 | — |
| 22 | TPR_PCT_4 | NUMERIC(18,4) | No | — | RDB$6353 | — |
| 23 | TPR_INV_LOW_5 | NUMERIC(18,2) | No | — | RDB$6354 | — |
| 24 | TPR_INV_HI_5 | NUMERIC(18,2) | No | — | RDB$6355 | — |
| 25 | TPR_PCT_5 | NUMERIC(18,4) | No | — | RDB$6356 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| TARIFF_POI_RATES_KEY | TPR_TARIFF_ID, TPR_EFF_DT, TPR_ACCOUNT, TPR_TYPE, TPR_KEYWORD, TPR_LOW_ZIP, TPR_HI_ZIP, TPR_STATE_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R264 | TARIFF_POI_RATES.(TPR_TARIFF_ID, TPR_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
