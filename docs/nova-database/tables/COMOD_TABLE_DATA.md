# COMOD_TABLE_DATA

Pricing and tariffs. Comod Table Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTD_REC_NUM, CTD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTD_REC_NUM | BIGINT | Yes | — | RDB$3512 | — |
| 2 | CTD_SEQ_NUM | SMALLINT | Yes | — | RDB$3513 | — |
| 3 | CTD_LOW | INTEGER | No | — | RDB$3514 | — |
| 4 | CTD_HIGH | INTEGER | No | — | RDB$3515 | — |
| 5 | CTD_RATE | NUMERIC(9,2) | No | — | RDB$3516 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTD_KEY | CTD_REC_NUM, CTD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R044 | COMOD_TABLE_DATA.(CTD_REC_NUM) | COMOD_TARIFF_TABLE.(CTT_TABLE_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
