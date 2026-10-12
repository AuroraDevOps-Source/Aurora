# CT_DQ_DESC

Codes and configuration. Ct Dq Desc records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTDD_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTDD_CODE | VARCHAR(4) | Yes | — | RDB$2145 | NONE/NONE |
| 2 | CTDD_DESC | VARCHAR(250) | No | — | RDB$2146 | NONE/NONE |
| 3 | CTDD_EXPIRE_NOTIFY_IND | VARCHAR(1) | No | — | RDB$3316 | NONE/NONE |
| 4 | CTDD_NOTIFY_DAYS | INTEGER | No | — | RDB$3317 | — |
| 5 | CTDD_NOTIFY_INTERVAL | INTEGER | No | — | RDB$3324 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTDD_PRIM_KEY | CTDD_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R074 | DRIVER_QUAL.(DQ_CODE) | CT_DQ_DESC.(CTDD_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
