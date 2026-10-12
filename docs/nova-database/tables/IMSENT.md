# IMSENT

Claims and receipts. Imsent records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (IST_SHIPNUM, IST_DOC_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | IST_SHIPNUM | INTEGER | Yes | — | RDB$5334 | — |
| 2 | IST_DOC_TYPE | VARCHAR(12) | Yes | — | RDB$5335 | NONE/NONE |
| 3 | IST_SENT_DT | TIMESTAMP | No | — | RDB$5336 | — |
| 4 | IST_RUN_NUM | INTEGER | No | — | RDB$5342 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IMSENT_KEY | IST_SHIPNUM, IST_DOC_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R144 | IMSENT.(IST_SHIPNUM) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
