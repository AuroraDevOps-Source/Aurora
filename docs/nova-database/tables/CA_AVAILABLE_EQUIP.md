# CA_AVAILABLE_EQUIP

Accounts and carriers. Ca Available Equip records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CAAE_ACCT_ID, CAAE_EQUIP_DESC_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 2 | CAAE_EQUIP_DESC_ID | INTEGER | Yes | — | RDB$5405 | — |
| 3 | CAAE_ACCT_ID | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CA_AVAILABLE_EQUIP | CAAE_ACCT_ID, CAAE_EQUIP_DESC_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R028 | CA_AVAILABLE_EQUIP.(CAAE_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R029 | CA_AVAILABLE_EQUIP.(CAAE_EQUIP_DESC_ID) | CT_DESC.(CTD_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
