# MC_QUEUES

Integration and reference. Mc Queues records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MCQ_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MCQ_ID | VARCHAR(25) | Yes | — | D_QUEUE_ID | NONE/NONE |
| 2 | MCQ_TYPE | CHAR(1) | Yes | — | RDB$4199 | NONE/NONE |
| 3 | MCQ_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | MCQ_QUEUES | BLOB SUB_TYPE 1 | No | — | RDB$4200 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| MCQ_PRIMARY_KEY | MCQ_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R105 | EQUIP_HEADER.(EH_MC_QUEUE_ID) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R151 | MC_QUEUE_ITEMS.(MCQI_ID) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R153 | MC_ROUTING.(MCR_ERROR_QUEUE) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R154 | MC_ROUTING.(MCR_QUEUE) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R276 | USERS.(U_MOBILE_MSG_QUEUE) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
