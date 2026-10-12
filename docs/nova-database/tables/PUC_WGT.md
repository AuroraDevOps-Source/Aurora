# PUC_WGT

Pricing and tariffs. Puc Wgt records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PUCW_RATE_NUM, PUCW_WGT_FROM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PUCW_RATE_NUM | INTEGER | Yes | — | RDB$6883 | — |
| 2 | PUCW_WGT_FROM | INTEGER | Yes | — | RDB$6884 | — |
| 3 | PUCW_WGT_TO | INTEGER | No | — | RDB$6885 | — |
| 4 | PUCW_RATE | NUMERIC(8,5) | No | — | RDB$6886 | — |
| 5 | PUCW_5AX_RATE | NUMERIC(8,5) | No | — | RDB$6887 | — |
| 6 | PUCW_6AX_RATE | NUMERIC(8,5) | No | — | RDB$6888 | — |
| 7 | PUCW_7AX_RATE | NUMERIC(8,5) | No | — | RDB$6889 | — |
| 8 | PUCW_8AX_RATE | NUMERIC(8,5) | No | — | RDB$6890 | — |
| 9 | PUCW_9AX_RATE | NUMERIC(8,5) | No | — | RDB$6891 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PUCWGT_KEY | PUCW_RATE_NUM, PUCW_WGT_FROM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R259 | PUC_WGT.(PUCW_RATE_NUM) | PUC_RATES.(PUC_RATE_NUM) | High | Trigger PUC_RATE_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
