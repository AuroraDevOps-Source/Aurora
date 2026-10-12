# PTB_TAR_PRIO

Pricing and tariffs. Ptb Tar Prio records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PTBT_REC_NUM, PTBT_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PTBT_REC_NUM | BIGINT | Yes | — | RDB$2006 | — |
| 2 | PTBT_SEQ_NUM | SMALLINT | Yes | — | RDB$2007 | — |
| 3 | PTBT_TARIFF | VARCHAR(6) | No | — | RDB$2008 | NONE/NONE |
| 5 | PTBT_RULE | CHAR(1) | No | — | RDB$2010 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PTBT_KEY | PTBT_REC_NUM, PTBT_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R258 | PTB_TAR_PRIO.(PTBT_REC_NUM) | PRICE_TB.(PTB_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
