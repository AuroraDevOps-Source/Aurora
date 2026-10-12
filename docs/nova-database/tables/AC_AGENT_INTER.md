# AC_AGENT_INTER

Accounts and carriers. Ac Agent Inter records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ICA_D83_REC_NUM, ICA_D83_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ICA_D83_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$2173 | — |
| 2 | ICA_D83_SEQ_NUM | SMALLINT | Yes | — | RDB$2174 | — |
| 3 | ICA_D83_INTER | VARCHAR(6) | No | — | RDB$2175 | NONE/NONE |
| 4 | ICA_D83_ADJ_RULE | CHAR(1) | No | — | RDB$2176 | NONE/NONE |
| 5 | ICA_D83_ADJ_PERC | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 6 | ICA_D83_ADJ_DIR | CHAR(1) | No | — | RDB$2178 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ICA_D83_KEY | ICA_D83_REC_NUM, ICA_D83_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R014 | AC_AGENT_INTER.(ICA_D83_REC_NUM) | AC_AGENT.(ICA_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
