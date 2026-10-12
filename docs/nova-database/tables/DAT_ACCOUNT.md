# DAT_ACCOUNT

Integration and reference. Dat Account records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DA_NOVA_UID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DA_HISTORY_DAYS | INTEGER | No | — | RDB$6061 | — |
| 2 | DA_LOGIN_ID | VARCHAR(16) | Yes | — | RDB$6062 | NONE/NONE |
| 3 | DA_NOVA_UID | VARCHAR(8) | Yes | — | RDB$6063 | NONE/NONE |
| 4 | DA_PASSWORD | VARCHAR(30) | Yes | — | RDB$6064 | NONE/NONE |
| 5 | DA_POSTER_DISPLAY_NAME | VARCHAR(8) | No | — | RDB$6065 | NONE/NONE |
| 6 | DA_PROCESS_DELAY | INTEGER | No | — | RDB$6066 | — |
| 7 | DA_REQUEST_URL | VARCHAR(255) | Yes | — | RDB$6067 | NONE/NONE |
| 8 | DA_RESPONSE_URL | VARCHAR(255) | Yes | — | RDB$6068 | NONE/NONE |
| 9 | DA_USE_DAT | CHAR(1) | No | — | RDB$6069 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_DAT_ACCOUNT_NOVA_UID | DA_NOVA_UID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R060 | DAT_ACCOUNT.(DA_NOVA_UID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
