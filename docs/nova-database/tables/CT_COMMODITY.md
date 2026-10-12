# CT_COMMODITY

Codes and configuration. Ct Commodity records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTCD_COMMOD). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTCD_COMMOD | VARCHAR(24) | Yes | — | RDB$7045 | NONE/NONE |
| 2 | CTCD_DESC | VARCHAR(36) | No | — | RDB$7046 | NONE/NONE |
| 3 | CTCD_QTY_DESC | VARCHAR(24) | No | — | RDB$7047 | NONE/NONE |
| 4 | CTCD_KEYWORD | VARCHAR(8) | No | — | RDB$7070 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_COMMODITY | CTCD_COMMOD | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R049 | CT_COMMODITY.(CTCD_KEYWORD) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
