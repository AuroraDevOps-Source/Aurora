# EQUIP_PM

Equipment and fuel. Equip Pm records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (EQPM_ID, EQPM_PGM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | EQPM_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | EQPM_PGM | VARCHAR(1) | Yes | — | RDB$4572 | NONE/NONE |
| 3 | EQPM_CODE | VARCHAR(6) | No | — | RDB$4573 | NONE/NONE |
| 4 | EQPM_LAST_ODO | NUMERIC(10,0) | No | — | RDB$4574 | — |
| 5 | EQPM_LAST_DT | DATE | No | — | RDB$4575 | — |
| 6 | EQPM_LAST_INIT | VARCHAR(12) | No | — | RDB$4576 | NONE/NONE |
| 7 | EQPM_COMMENT | VARCHAR(256) | No | — | RDB$4577 | NONE/NONE |
| 8 | EQPM_LAST_NOTIFY_TIME | TIMESTAMP | No | — | RDB$5108 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| EQUIP_PM_PGM_CODE_KEY | EQPM_CODE, EQPM_ID | No | Yes | ASC | — |
| PK_EQUIP_PM | EQPM_ID, EQPM_PGM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R118 | EQUIP_PM.(EQPM_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
