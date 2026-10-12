# EQUIP_EVENT

Equipment and fuel. Equip Event records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (EQE_ID, EQE_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | EQE_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | EQE_SEQ | SMALLINT | Yes | — | RDB$2770 | — |
| 3 | EQE_DATE | DATE | No | — | RDB$2771 | — |
| 4 | EQE_CODE | VARCHAR(4) | No | — | RDB$2772 | NONE/NONE |
| 5 | EQE_DESC | VARCHAR(2048) | No | — | RDB$2773 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| EQUIP_EVENT_PRIM_KEY | EQE_ID, EQE_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R101 | EQUIP_EVENT.(EQE_CODE) | CT_EVENT_CODES.(CTEC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R102 | EQUIP_EVENT.(EQE_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
