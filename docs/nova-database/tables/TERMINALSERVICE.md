# TERMINALSERVICE

Locations and routing. Terminalservice records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TS_ORIGLOC, TS_DESTLOC, TS_VIA1, TS_VIA2). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TS_ORIGLOC | VARCHAR(12) | Yes | — | RDB$6100 | NONE/NONE |
| 2 | TS_DESTLOC | VARCHAR(12) | Yes | — | RDB$6101 | NONE/NONE |
| 3 | TS_VIA1 | VARCHAR(12) | Yes | — | RDB$6102 | NONE/NONE |
| 4 | TS_VIA2 | VARCHAR(12) | Yes | — | RDB$6103 | NONE/NONE |
| 5 | TS_DAYS | INTEGER | No | — | RDB$6104 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| TERMINALSERVICE_PRIM_KEY | TS_ORIGLOC, TS_DESTLOC, TS_VIA1, TS_VIA2 | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R266 | TERMINALSERVICE.(TS_DESTLOC) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R267 | TERMINALSERVICE.(TS_ORIGLOC) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R268 | TERMINALSERVICE.(TS_VIA1) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R269 | TERMINALSERVICE.(TS_VIA2) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
