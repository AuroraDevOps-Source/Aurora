# AC_SKU

Accounts and carriers. Ac Sku records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (AKU_ACCT_ID, AKU_SKU). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | AKU_ACCT_ID | VARCHAR(12) | Yes | — | RDB$7257 | NONE/NONE |
| 2 | AKU_SKU | VARCHAR(35) | Yes | — | RDB$7258 | NONE/NONE |
| 3 | AKU_DESC | VARCHAR(128) | No | — | RDB$7259 | NONE/NONE |
| 4 | AKU_CUBES | VARCHAR(20) | No | — | RDB$7260 | NONE/NONE |
| 5 | AKU_WGT | NUMERIC(15,3) | No | — | RDB$7261 | — |
| 6 | AKU_KILOS | NUMERIC(15,3) | No | — | RDB$7262 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_AC_SKU | AKU_ACCT_ID, AKU_SKU | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R022 | AC_SKU.(AKU_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
