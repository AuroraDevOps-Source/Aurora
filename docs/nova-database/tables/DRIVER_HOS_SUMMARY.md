# DRIVER_HOS_SUMMARY

Drivers and settlement. Driver Hos Summary records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DHS_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DHS_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | DHS_CALCULATED_TIME | TIMESTAMP | No | — | RDB$4106 | — |
| 3 | DHS_RECEIVED_TIME | TIMESTAMP | No | — | RDB$4107 | — |
| 4 | DHS_CURRENT_STATUS | VARCHAR(20) | No | — | RDB$4108 | NONE/NONE |
| 5 | DHS_CURRENT_SUBSTATUS | VARCHAR(20) | No | — | RDB$4109 | NONE/NONE |
| 6 | DHS_CURRENT_STATUS_TIME | TIMESTAMP | No | — | RDB$4110 | — |
| 7 | DHS_LATITUDE | DOUBLE PRECISION | No | — | RDB$4111 | — |
| 8 | DHS_LONGITUDE | DOUBLE PRECISION | No | — | RDB$4112 | — |
| 9 | DHS_CERTIFIED_DATE | TIMESTAMP | No | — | RDB$4113 | — |
| 10 | DHS_REMAINING_DRIVING | INTEGER | No | — | RDB$4114 | — |
| 11 | DHS_REMAINING_ONDUTY | INTEGER | No | — | RDB$4115 | — |
| 12 | DHS_REMAINING_TOTAL_ONDUTY | INTEGER | No | — | RDB$4116 | — |
| 13 | DHS_REMAINING_BEFORE_SPLIT | INTEGER | No | — | RDB$4117 | — |
| 14 | DHS_REMAINING_BEFORE_DAILY | INTEGER | No | — | RDB$4118 | — |
| 15 | DHS_REMAINING_BEFORE_WEEKLY | INTEGER | No | — | RDB$4119 | — |
| 16 | DHS_DRIVE_AVAIL_TOMORROW | INTEGER | No | — | RDB$4120 | — |
| 17 | DHS_REMAINING_EFF_DRIVE | INTEGER | No | — | RDB$4121 | — |
| 18 | DHS_ONDUTY_TOMORROW | INTEGER | No | — | RDB$4122 | — |
| 19 | DHS_DRIVING_SINCE_RESET | INTEGER | No | — | RDB$4123 | — |
| 20 | DHS_ONDUTY_SINCE_RESET | INTEGER | No | — | RDB$4124 | — |
| 21 | DHS_TOTAL_ONDUTY_SINCE_RESET | INTEGER | No | — | RDB$4125 | — |
| 22 | DHS_WEEKLY_END_TIME | TIMESTAMP | No | — | RDB$4126 | — |
| 23 | DHS_SPLIT_RESET_END_TIME | TIMESTAMP | No | — | RDB$4127 | — |
| 24 | DHS_DAILY_RESET_END_TIME | TIMESTAMP | No | — | RDB$4128 | — |
| 25 | DHS_MANDATORY_BREAK_VIOLATION | VARCHAR(1) | No | — | D_YESNO | NONE/NONE |
| 26 | DHS_MANDATORY_BREAK_END_TIME | TIMESTAMP | No | — | RDB$4129 | — |
| 27 | DHS_REMAINING_MAX_DRIVING_TIME | INTEGER | No | — | RDB$4130 | — |
| 28 | DHS_REMAINING_BEFORE_BREAK | INTEGER | No | — | RDB$4131 | — |
| 29 | DHS_OPERATIONAL_EXEMPTION | VARCHAR(20) | No | — | RDB$4132 | NONE/NONE |
| 30 | DHS_16HR_ELIGIBLE | VARCHAR(1) | No | — | D_YESNO | NONE/NONE |
| 31 | DHS_WEEKLY_START_TIME | TIMESTAMP | No | — | RDB$4133 | — |
| 32 | DHS_TODAY_ONDUTY | INTEGER | No | — | RDB$4272 | — |
| 33 | DHS_TODAY_OFFDUTY | INTEGER | No | — | RDB$4273 | — |
| 34 | DHS_TODAY_SLEEPER | INTEGER | No | — | RDB$4274 | — |
| 35 | DHS_TODAY_DRIVING | INTEGER | No | — | RDB$4275 | — |
| 36 | DHS_YESTERDAY_ONDUTY | INTEGER | No | — | RDB$4276 | — |
| 37 | DHS_YESTERDAY_OFFDUTY | INTEGER | No | — | RDB$4277 | — |
| 38 | DHS_YESTERDAY_SLEEPER | INTEGER | No | — | RDB$4278 | — |
| 39 | DHS_YESTERDAY_DRIVING | INTEGER | No | — | RDB$4279 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_DRIVER_HOS_SUMMARY | DHS_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R069 | DRIVER_HOS_SUMMARY.(DHS_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
