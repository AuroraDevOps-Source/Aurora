# HAZ_DESC_DATA

Integration and reference. Haz Desc Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (HAZD_REC_NUM, HAZD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | HAZD_REC_NUM | BIGINT | Yes | — | RDB$2874 | — |
| 2 | HAZD_SEQ_NUM | SMALLINT | Yes | — | RDB$2875 | — |
| 3 | HAZD_DESC_LINE | VARCHAR(36) | No | — | RDB$2876 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| HAZD_KEY | HAZD_REC_NUM, HAZD_SEQ_NUM | Yes | Yes | ASC | — |
| HAZD_REC_NUM_KEY | HAZD_REC_NUM | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R139 | HAZ_DESC_DATA.(HAZD_REC_NUM) | HAZ.(HAZ_DESC_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
