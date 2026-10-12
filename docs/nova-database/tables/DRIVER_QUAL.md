# DRIVER_QUAL

Drivers and settlement. Driver Qual records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DQ_ID, DQ_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DQ_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | DQ_CODE | VARCHAR(4) | Yes | — | RDB$2139 | NONE/NONE |
| 3 | DQ_STATUS | VARCHAR(5) | No | — | RDB$2140 | NONE/NONE |
| 4 | DQ_STAT_INIT | VARCHAR(12) | No | — | RDB$2141 | NONE/NONE |
| 5 | DQ_DATE | DATE | No | — | RDB$2142 | — |
| 6 | DQ_EXP_DT | DATE | No | — | RDB$2143 | — |
| 7 | DQ_COMMENT | VARCHAR(1024) | No | — | RDB$2144 | NONE/NONE |
| 9 | DQ_LAST_NOTIFY_TIME | TIMESTAMP | No | — | RDB$3321 | — |
| 10 | DQ_EXP_NOTIFY_TYPE | VARCHAR(1) | No | — | RDB$3322 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DQ_PRIM_KEY | DQ_ID, DQ_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R074 | DRIVER_QUAL.(DQ_CODE) | CT_DQ_DESC.(CTDD_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R075 | DRIVER_QUAL.(DQ_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |
| R076 | DRIVER_QUAL.(DQ_STATUS) | CT_DQ_STATUS.(CTDS_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
