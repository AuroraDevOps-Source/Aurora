# AC_RPT_SHIPMENTS

Accounts and carriers. Ac Rpt Shipments records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: **None declared**. No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ACRS_ID | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |
| 2 | ACRS_ITEM_NUMBER | VARCHAR(16) | Yes | — | RDB$5077 | NONE/NONE |
| 3 | ACRS_CLASS | VARCHAR(8) | No | — | D_KEYWORD | NONE/NONE |
| 4 | ACRS_SHIPMENT_NUMBER | BIGINT | Yes | — | RDB$4239 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_AC_RPT_SHIPMENTS | ACRS_ID, ACRS_ITEM_NUMBER | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R020 | AC_RPT_SHIPMENTS.(ACRS_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R021 | AC_RPT_SHPMNT_LINES.(ACRSL_SHIPMENT_NUMBER) | AC_RPT_SHIPMENTS.(ACRS_SHIPMENT_NUMBER) | High | Trigger AC_RPT_SHIPMENTS_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

`AC_RPT_SHIPMENTS_AFTER_DELETE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
