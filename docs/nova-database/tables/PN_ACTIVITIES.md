# PN_ACTIVITIES

Integration and reference. Pn Activities records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PNA_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PNA_ID | INTEGER | Yes | — | RDB$5133 | — |
| 2 | PNA_TYPE | INTEGER | No | — | RDB$5134 | — |
| 3 | PNA_FIRST_TIME | TIMESTAMP | No | — | RDB$5135 | — |
| 4 | PNA_LAST_TIME | TIMESTAMP | No | — | RDB$5136 | — |
| 5 | PNA_NEXT_TIME | TIMESTAMP | No | — | RDB$5137 | — |
| 6 | PNA_AURORA_MSG_ID | BIGINT | No | — | RDB$5138 | — |
| 7 | PNA_MSG_ID | BIGINT | No | — | RDB$5139 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_PN_ACTIVITIES | PNA_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R239 | PN_ACTIVITIES.(PNA_AURORA_MSG_ID) | MC_MESSAGES.(MM_MSG_NUMBER) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`PN_ACTIVITIES_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
