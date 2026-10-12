# CT_FLEETS

Codes and configuration. Ct Fleets records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTF_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTF_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | CTF_CODE | VARCHAR(4) | No | — | D_FLEET_CODE | NONE/NONE |
| 3 | CTF_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | CTF_UNIT_TYPE | BIGINT | No | — | D_SYSID | — |
| 5 | CTF_PM_CODE_1 | VARCHAR(6) | No | — | RDB$4512 | NONE/NONE |
| 6 | CTF_PM_DESC_1 | VARCHAR(128) | No | — | RDB$4513 | NONE/NONE |
| 7 | CTF_PM_MIN_MI_1 | INTEGER | No | — | RDB$4514 | — |
| 8 | CTF_PM_MAX_MI_1 | INTEGER | No | — | RDB$4515 | — |
| 9 | CTF_PM_DAYS_1 | INTEGER | No | — | RDB$4516 | — |
| 10 | CTF_PM_CODE_2 | VARCHAR(6) | No | — | RDB$4517 | NONE/NONE |
| 11 | CTF_PM_DESC_2 | VARCHAR(128) | No | — | RDB$4518 | NONE/NONE |
| 12 | CTF_PM_MIN_MI_2 | INTEGER | No | — | RDB$4519 | — |
| 13 | CTF_PM_MAX_MI_2 | INTEGER | No | — | RDB$4520 | — |
| 14 | CTF_PM_DAYS_2 | INTEGER | No | — | RDB$4521 | — |
| 15 | CTF_PM_CODE_3 | VARCHAR(6) | No | — | RDB$4522 | NONE/NONE |
| 16 | CTF_PM_DESC_3 | VARCHAR(128) | No | — | RDB$4523 | NONE/NONE |
| 17 | CTF_PM_MIN_MI_3 | INTEGER | No | — | RDB$4524 | — |
| 18 | CTF_PM_MAX_MI_3 | INTEGER | No | — | RDB$4525 | — |
| 19 | CTF_PM_DAYS_3 | INTEGER | No | — | RDB$4526 | — |
| 20 | CTF_PM_CODE_4 | VARCHAR(6) | No | — | RDB$4527 | NONE/NONE |
| 21 | CTF_PM_DESC_4 | VARCHAR(128) | No | — | RDB$4528 | NONE/NONE |
| 22 | CTF_PM_MIN_MI_4 | INTEGER | No | — | RDB$4529 | — |
| 23 | CTF_PM_MAX_MI_4 | INTEGER | No | — | RDB$4530 | — |
| 24 | CTF_PM_DAYS_4 | INTEGER | No | — | RDB$4531 | — |
| 25 | CTF_PM_CODE_5 | VARCHAR(6) | No | — | RDB$4532 | NONE/NONE |
| 26 | CTF_PM_DESC_5 | VARCHAR(128) | No | — | RDB$4533 | NONE/NONE |
| 27 | CTF_PM_MIN_MI_5 | INTEGER | No | — | RDB$4534 | — |
| 28 | CTF_PM_MAX_MI_5 | INTEGER | No | — | RDB$4535 | — |
| 29 | CTF_PM_DAYS_5 | INTEGER | No | — | RDB$4536 | — |
| 30 | CTF_PM_CODE_6 | VARCHAR(6) | No | — | RDB$4537 | NONE/NONE |
| 31 | CTF_PM_DESC_6 | VARCHAR(128) | No | — | RDB$4538 | NONE/NONE |
| 32 | CTF_PM_MIN_MI_6 | INTEGER | No | — | RDB$4539 | — |
| 33 | CTF_PM_MAX_MI_6 | INTEGER | No | — | RDB$4540 | — |
| 34 | CTF_PM_DAYS_6 | INTEGER | No | — | RDB$4541 | — |
| 35 | CTF_PM_NOTIFY_IND_1 | CHAR(1) | No | — | RDB$5084 | NONE/NONE |
| 36 | CTF_PM_NOTIFY_DAYS_1 | INTEGER | No | — | RDB$5085 | — |
| 37 | CTF_PM_NOTIFY_MI_1 | INTEGER | No | — | RDB$5086 | — |
| 38 | CTF_PM_NOTIFY_INTERVAL_1 | INTEGER | No | — | RDB$5087 | — |
| 39 | CTF_PM_NOTIFY_IND_2 | CHAR(1) | No | — | RDB$5088 | NONE/NONE |
| 40 | CTF_PM_NOTIFY_DAYS_2 | INTEGER | No | — | RDB$5089 | — |
| 41 | CTF_PM_NOTIFY_MI_2 | INTEGER | No | — | RDB$5090 | — |
| 42 | CTF_PM_NOTIFY_INTERVAL_2 | INTEGER | No | — | RDB$5091 | — |
| 43 | CTF_PM_NOTIFY_IND_3 | CHAR(1) | No | — | RDB$5092 | NONE/NONE |
| 44 | CTF_PM_NOTIFY_DAYS_3 | INTEGER | No | — | RDB$5093 | — |
| 45 | CTF_PM_NOTIFY_MI_3 | INTEGER | No | — | RDB$5094 | — |
| 46 | CTF_PM_NOTIFY_INTERVAL_3 | INTEGER | No | — | RDB$5095 | — |
| 47 | CTF_PM_NOTIFY_IND_4 | CHAR(1) | No | — | RDB$5096 | NONE/NONE |
| 48 | CTF_PM_NOTIFY_DAYS_4 | INTEGER | No | — | RDB$5097 | — |
| 49 | CTF_PM_NOTIFY_MI_4 | INTEGER | No | — | RDB$5098 | — |
| 50 | CTF_PM_NOTIFY_INTERVAL_4 | INTEGER | No | — | RDB$5099 | — |
| 51 | CTF_PM_NOTIFY_IND_5 | CHAR(1) | No | — | RDB$5100 | NONE/NONE |
| 52 | CTF_PM_NOTIFY_DAYS_5 | INTEGER | No | — | RDB$5101 | — |
| 53 | CTF_PM_NOTIFY_MI_5 | INTEGER | No | — | RDB$5102 | — |
| 54 | CTF_PM_NOTIFY_INTERVAL_5 | INTEGER | No | — | RDB$5103 | — |
| 55 | CTF_PM_NOTIFY_IND_6 | CHAR(1) | No | — | RDB$5104 | NONE/NONE |
| 56 | CTF_PM_NOTIFY_DAYS_6 | INTEGER | No | — | RDB$5105 | — |
| 57 | CTF_PM_NOTIFY_MI_6 | INTEGER | No | — | RDB$5106 | — |
| 58 | CTF_PM_NOTIFY_INTERVAL_6 | INTEGER | No | — | RDB$5107 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_FLEETS | CTF_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R104 | EQUIP_HEADER.(EH_FLEET) | CT_FLEETS.(CTF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`CT_FLEETS_BI0`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
