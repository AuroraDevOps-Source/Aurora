# QUAL128

Integration and reference. Qual128 records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (Q_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Q_CODE | VARCHAR(4) | Yes | — | RDB$3852 | NONE/NONE |
| 2 | Q_SEQUENCE | BIGINT | Yes | — | RDB$3853 | — |
| 3 | Q_DESCRIPTION | VARCHAR(30) | No | — | RDB$3854 | NONE/NONE |
| 4 | Q_I | CHAR(1) | No | — | RDB$3855 | NONE/NONE |
| 5 | Q_T | CHAR(1) | No | — | RDB$3856 | NONE/NONE |
| 6 | Q_L | CHAR(1) | No | — | RDB$3857 | NONE/NONE |
| 7 | Q_SEND_TO_MOBILE | CHAR(1) | No | — | RDB$5348 | NONE/NONE |
| 8 | Q_PRINT | CHAR(1) | No | — | RDB$5563 | NONE/NONE |
| 9 | Q_SEND_EDI | CHAR(1) | No | — | RDB$5625 | NONE/NONE |
| 10 | Q_204_TO_PU_NOTES | CHAR(1) | No | — | RDB$6050 | NONE/NONE |
| 11 | Q_SHOW_ON_DPCH_BOARD | CHAR(1) | No | — | RDB$6965 | NONE/NONE |
| 12 | Q_AF | CHAR(1) | No | — | RDB$7132 | NONE/NONE |
| 13 | Q_AG | CHAR(1) | No | — | RDB$7133 | NONE/NONE |
| 14 | Q_NO | CHAR(1) | No | — | RDB$7134 | NONE/NONE |
| 15 | Q_WH | CHAR(1) | No | — | RDB$7135 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| QUAL128_KEY | Q_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R210 | OPS_NUMBERS.(OPN_QUALIFIER) | QUAL128.(Q_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
