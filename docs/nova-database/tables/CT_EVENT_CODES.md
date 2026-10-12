# CT_EVENT_CODES

Codes and configuration. Ct Event Codes records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTEC_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTEC_CODE | VARCHAR(4) | Yes | — | RDB$6458 | NONE/NONE |
| 2 | CTEC_DESC | VARCHAR(80) | No | — | RDB$6459 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CT_EVENT_CODES_PRIM_KEY | CTEC_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R101 | EQUIP_EVENT.(EQE_CODE) | CT_EVENT_CODES.(CTEC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
