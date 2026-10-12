# AC_RPT_SHPMNT_LINES

Accounts and carriers. Ac Rpt Shpmnt Lines records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ACRSL_SHIPMENT_NUMBER, ACRSL_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ACRSL_SHIPMENT_NUMBER | BIGINT | Yes | — | RDB$4240 | — |
| 2 | ACRSL_SEQUENCE | INTEGER | Yes | — | RDB$4241 | — |
| 3 | ACRSL_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | ACRSL_KEYWORD | VARCHAR(8) | No | — | D_KEYWORD | NONE/NONE |
| 5 | ACRSL_WEIGHT | NUMERIC(15,3) | No | — | D_WEIGHT | — |
| 6 | ACRSL_CUBE | VARCHAR(20) | No | — | RDB$5337 | NONE/NONE |
| 7 | ACRSL_PIECES | NUMERIC(15,3) | No | — | D_PIECES | — |
| 8 | ACRSL_RATE | NUMERIC(15,4) | No | — | D_RATE | — |
| 9 | ACRSL_AMOUNT | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 10 | ACRSL_PALLETS | INTEGER | No | — | RDB$5338 | — |
| 11 | ACRSL_PALLET_POS | INTEGER | No | — | RDB$5339 | — |
| 13 | ACRSL_STACK | VARCHAR(1) | No | — | RDB$5341 | NONE/NONE |
| 14 | ACRSL_KILOS | NUMERIC(15,3) | No | — | RDB$6384 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_AC_RPT_SHPMNT_LINES | ACRSL_SHIPMENT_NUMBER, ACRSL_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R021 | AC_RPT_SHPMNT_LINES.(ACRSL_SHIPMENT_NUMBER) | AC_RPT_SHIPMENTS.(ACRS_SHIPMENT_NUMBER) | High | Trigger AC_RPT_SHIPMENTS_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
