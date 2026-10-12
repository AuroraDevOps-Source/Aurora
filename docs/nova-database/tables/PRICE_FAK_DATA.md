# PRICE_FAK_DATA

Pricing and tariffs. Price Fak Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PFAKD_REC_NUM, PFAKD_SEQ_NUM, PFAKD_RULE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PFAKD_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$2025 | — |
| 2 | PFAKD_SEQ_NUM | SMALLINT | Yes | — | RDB$2026 | — |
| 3 | PFAKD_ACT_CLASS | VARCHAR(4) | No | — | RDB$2027 | NONE/NONE |
| 4 | PFAKD_AS_CLASS | VARCHAR(4) | No | — | RDB$2028 | NONE/NONE |
| 5 | PFAKD_RULE | VARCHAR(8) | Yes | — | RDB$2029 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PFAKD_KEY | PFAKD_REC_NUM, PFAKD_SEQ_NUM, PFAKD_RULE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R244 | PRICE_FAK_DATA.(PFAKD_REC_NUM) | PRICE_FAK.(PFAK_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
