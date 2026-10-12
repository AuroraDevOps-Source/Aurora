# CT_CNTR_TRACK

Codes and configuration. Ct Cntr Track records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CCT_KEY). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CCT_KEY | VARCHAR(10) | Yes | — | RDB$6197 | NONE/NONE |
| 2 | CCT_URL | VARCHAR(255) | No | — | RDB$6198 | NONE/NONE |
| 3 | CCT_API_KEY | VARCHAR(50) | No | — | RDB$6199 | NONE/NONE |
| 4 | CCT_USER_NAME | VARCHAR(50) | No | — | RDB$6200 | NONE/NONE |
| 5 | CCT_PASSWORD | VARCHAR(50) | No | — | RDB$6201 | NONE/NONE |
| 6 | CCT_POLL_TIME1 | TIME | No | — | RDB$6203 | — |
| 7 | CCT_POLL_TIME2 | TIME | No | — | RDB$6204 | — |
| 8 | CCT_POLL_TIME3 | TIME | No | — | RDB$6205 | — |
| 9 | CCT_POLL_TIME4 | TIME | No | — | RDB$6206 | — |
| 10 | CCT_REGISTER_INTERVAL | INTEGER | No | — | RDB$6391 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_CNTR_TRACK | CCT_KEY | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
