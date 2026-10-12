# PTB_INTRA

Pricing and tariffs. Ptb Intra records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PTBI_REC_NUM, PTBI_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PTBI_REC_NUM | BIGINT | Yes | — | RDB$2011 | — |
| 2 | PTBI_SEQ_NUM | SMALLINT | Yes | — | RDB$2012 | — |
| 3 | PTBI_ST | CHAR(2) | No | — | RDB$2013 | NONE/NONE |
| 4 | PTBI_TAR | VARCHAR(6) | No | — | RDB$2014 | NONE/NONE |
| 6 | PTBI_T_RULE | CHAR(1) | No | — | RDB$2016 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PTBI_KEY | PTBI_REC_NUM, PTBI_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R256 | PTB_INTRA.(PTBI_REC_NUM) | PRICE_TB.(PTB_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
