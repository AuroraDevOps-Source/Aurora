# CT_WHSE_LOCS

Containers and warehouse. Ct Whse Locs records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: **None declared**. No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTWHL_WHSE_ID | VARCHAR(12) | Yes | — | RDB$6028 | NONE/NONE |
| 2 | CTWHL_INVID | VARCHAR(12) | No | — | RDB$6029 | NONE/NONE |
| 3 | CTWHL_NAME | VARCHAR(40) | No | — | RDB$6030 | NONE/NONE |
| 4 | CTWHL_ADR1 | VARCHAR(50) | No | — | RDB$6031 | NONE/NONE |
| 5 | CTWHL_ADR2 | VARCHAR(50) | No | — | RDB$6032 | NONE/NONE |
| 6 | CTWHL_CITY | VARCHAR(35) | No | — | RDB$6033 | NONE/NONE |
| 7 | CTWHL_ST | VARCHAR(2) | No | — | RDB$6034 | NONE/NONE |
| 8 | CTWHL_ZIP | VARCHAR(9) | No | — | RDB$6035 | NONE/NONE |
| 9 | CTWHL_PHONE | VARCHAR(32) | No | — | RDB$6036 | NONE/NONE |
| 10 | CTWHL_CONTACT | VARCHAR(40) | No | — | RDB$6037 | NONE/NONE |
| 11 | CTWHL_PRINTER | VARCHAR(150) | No | — | RDB$6038 | NONE/NONE |
| 12 | CTWHL_EMAIL | VARCHAR(150) | No | — | RDB$6039 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IDX_CT_WHSE_LOCS | CTWHL_WHSE_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R058 | CT_WHSE_UNITS.(CTWHU_LOC) | CT_WHSE_LOCS.(CTWHL_WHSE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
