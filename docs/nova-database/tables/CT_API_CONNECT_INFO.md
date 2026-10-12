# CT_API_CONNECT_INFO

Codes and configuration. Ct Api Connect Info records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTAI_ACCOUNT, CTAI_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTAI_ACCOUNT | VARCHAR(12) | Yes | — | RDB$7165 | NONE/NONE |
| 2 | CTAI_TYPE | VARCHAR(25) | Yes | — | RDB$7166 | NONE/NONE |
| 3 | CTAI_NAME | VARCHAR(128) | Yes | — | RDB$7167 | NONE/NONE |
| 4 | CTAI_CALL_TYPE | VARCHAR(25) | No | — | RDB$7168 | NONE/NONE |
| 5 | CTAI_URL | VARCHAR(512) | No | — | RDB$7169 | NONE/NONE |
| 6 | CTAI_USER | VARCHAR(256) | No | — | RDB$7170 | NONE/NONE |
| 7 | CTAI_PASSWORD | VARCHAR(256) | No | — | RDB$7171 | NONE/NONE |
| 8 | CTAI_KEY | VARCHAR(256) | No | — | RDB$7172 | NONE/NONE |
| 9 | CTAI_ENCODING | VARCHAR(25) | No | — | RDB$7173 | NONE/NONE |
| 10 | CTAI_AUTHENTICATION | VARCHAR(256) | No | — | RDB$7174 | NONE/NONE |
| 11 | CTAI_READ_TIMEOUT | INTEGER | No | — | RDB$7175 | — |
| 12 | CTAI_CONNECT_TIMEOUT | INTEGER | No | — | RDB$7176 | — |
| 13 | CTAI_PARAMETERS | VARCHAR(2560) | No | — | RDB$7177 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_API_CONNECT_INFO | CTAI_ACCOUNT, CTAI_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R048 | CT_API_CONNECT_INFO.(CTAI_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
