# MC_QUEUE_ITEMS

Integration and reference. Mc Queue Items records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MCQI_ID, MCQI_MSG_NUMBER). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MCQI_ID | VARCHAR(25) | Yes | — | D_QUEUE_ID | NONE/NONE |
| 2 | MCQI_MSG_NUMBER | BIGINT | Yes | — | RDB$4209 | — |
| 3 | MCQI_NEW_MESSAGE | CHAR(1) | No | — | RDB$4210 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| MCQI_MSG_KEY | MCQI_MSG_NUMBER | No | Yes | ASC | — |
| MCQI_PRIMARY_KEY | MCQI_ID, MCQI_MSG_NUMBER | Yes | Yes | ASC | — |
| MCQI_REVERSE_KEY | MCQI_ID, MCQI_MSG_NUMBER | No | Yes | DESC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R151 | MC_QUEUE_ITEMS.(MCQI_ID) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R152 | MC_QUEUE_ITEMS.(MCQI_MSG_NUMBER) | MC_MESSAGES.(MM_MSG_NUMBER) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`MCQI_AFTER_INSERT`, `MCQI_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
