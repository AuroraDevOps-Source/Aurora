# PUC_RATES

Pricing and tariffs. Puc Rates records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PUC_STATE, PUC_EFF_DATE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PUC_STATE | VARCHAR(2) | Yes | — | RDB$6879 | NONE/NONE |
| 2 | PUC_EFF_DATE | DATE | Yes | — | RDB$6880 | — |
| 3 | PUC_EXP_DATE | DATE | No | — | RDB$6881 | — |
| 4 | PUC_RATE_NUM | INTEGER | No | — | RDB$6882 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PUCRATE_KEY | PUC_STATE, PUC_EFF_DATE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R259 | PUC_WGT.(PUCW_RATE_NUM) | PUC_RATES.(PUC_RATE_NUM) | High | Trigger PUC_RATE_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

`PUC_RATE_AFTER_DELETE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
