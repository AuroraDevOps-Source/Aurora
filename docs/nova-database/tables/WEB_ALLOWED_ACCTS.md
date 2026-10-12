# WEB_ALLOWED_ACCTS

Users and access. Web Allowed Accts records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (WUAA_USER_ID, WUAA_ACCT_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | WUAA_USER_ID | VARCHAR(50) | Yes | — | RDB$4280 | NONE/NONE |
| 2 | WUAA_ACCT_ID | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |
| 3 | WUAA_LOCATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 4 | WUAA_ACCT_TYPE | INTEGER | No | — | RDB$4281 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_WEB_ALLOWED_ACCTS | WUAA_USER_ID, WUAA_ACCT_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R293 | WEB_ALLOWED_ACCTS.(WUAA_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R294 | WEB_ALLOWED_ACCTS.(WUAA_USER_ID) | WEB_USERS.(WU_USER_ID) | High | Trigger WEB_USER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
