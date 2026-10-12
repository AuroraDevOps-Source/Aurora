# CT_CLAIM_STATUS

Codes and configuration. Ct Claim Status records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTCS_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTCS_CODE | VARCHAR(2) | Yes | — | RDB$5714 | NONE/NONE |
| 2 | CTCS_DESC | VARCHAR(256) | No | — | RDB$5715 | NONE/NONE |
| 3 | CTCS_CLOSE_YN | CHAR(1) | No | — | RDB$5716 | NONE/NONE |
| 4 | CTCS_BGND_COLOR | INTEGER | No | — | RDB$5776 | — |
| 5 | CTCS_TEXT_COLOR | INTEGER | No | — | RDB$5777 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTCS_PRIM_KEY | CTCS_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R038 | CLAIM_HEADER.(CLH_STATUS) | CT_CLAIM_STATUS.(CTCS_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
