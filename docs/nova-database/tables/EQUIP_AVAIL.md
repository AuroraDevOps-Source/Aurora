# EQUIP_AVAIL

Equipment and fuel. Equip Avail records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (EQA_ID, EQA_DATE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | EQA_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | EQA_DATE | DATE | Yes | — | RDB$6512 | — |
| 3 | EQA_STATUS | BIGINT | No | — | D_SYSID | — |
| 4 | EQA_START_TIME | TIME | No | — | RDB$6513 | — |
| 5 | EQA_END_TIME | TIME | No | — | RDB$6514 | — |
| 6 | EQA_COMMENT | VARCHAR(512) | No | — | RDB$6515 | NONE/NONE |
| 7 | EQA_TYPE | INTEGER | No | — | RDB$6579 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_EQUIP_AVAIL | EQA_ID, EQA_DATE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R097 | EQUIP_AVAIL.(EQA_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R098 | EQUIP_AVAIL.(EQA_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
