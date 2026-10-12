# OPS_AG_LINES

Orders and manifests. Agent-specific line charges within an order.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OPAL_ORDER_ID, OPAL_SEQ, OPAL_OPA_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OPAL_ORDER_ID | BIGINT | Yes | — | RDB$5577 | — |
| 2 | OPAL_OPA_SEQUENCE | INTEGER | Yes | — | RDB$5578 | — |
| 3 | OPAL_SEQ | INTEGER | Yes | — | RDB$5579 | — |
| 4 | OPAL_KEYWORD | VARCHAR(8) | No | — | RDB$5580 | NONE/NONE |
| 5 | OPAL_DESC | VARCHAR(50) | No | — | RDB$5581 | NONE/NONE |
| 6 | OPAL_PIECES | NUMERIC(15,3) | No | — | RDB$5582 | — |
| 7 | OPAL_WEIGHT | NUMERIC(15,3) | No | — | RDB$5583 | — |
| 8 | OPAL_RATE | NUMERIC(15,4) | No | — | RDB$5584 | — |
| 9 | OPAL_AMOUNT | NUMERIC(16,2) | No | — | RDB$5585 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| OPS_AG_LINES_KEY | OPAL_ORDER_ID, OPAL_SEQ, OPAL_OPA_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R166 | OPS_AG_LINES.(OPAL_KEYWORD) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R167 | OPS_AG_LINES.(OPAL_ORDER_ID, OPAL_OPA_SEQUENCE) | OPS_AGENTS.(OPA_ORDER_ID, OPA_SEQUENCE) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

`OPS_AG_LINES_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
