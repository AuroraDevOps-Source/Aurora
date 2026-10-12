# MC_MESSAGES

Integration and reference. Mc Messages records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MM_MSG_NUMBER). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MM_MSG_NUMBER | BIGINT | Yes | — | RDB$4178 | — |
| 2 | MM_MSG_DATE | TIMESTAMP | No | — | RDB$4179 | — |
| 3 | MM_SYSTEM_TYPE | VARCHAR(1) | No | — | RDB$4180 | NONE/NONE |
| 4 | MM_FORM_NUMBER | BIGINT | No | — | RDB$4181 | — |
| 5 | MM_FORM_VERSION | BIGINT | No | — | RDB$4182 | — |
| 6 | MM_DIRECTION | VARCHAR(1) | No | — | RDB$4183 | NONE/NONE |
| 7 | MM_STATUS | VARCHAR(1) | No | — | RDB$4184 | NONE/NONE |
| 8 | MM_SUB_STATUS | VARCHAR(255) | No | — | RDB$4185 | NONE/NONE |
| 9 | MM_RAW_MESSAGE | BLOB SUB_TYPE 1 | No | — | RDB$4186 | NONE/NONE |
| 10 | MM_INITIALS | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 11 | MM_MSG_LINE1 | VARCHAR(255) | No | — | RDB$4187 | NONE/NONE |
| 12 | MM_MSG_REFERENCE | VARCHAR(50) | No | — | RDB$4188 | NONE/NONE |
| 13 | MM_MSG_PRIORITY | VARCHAR(1) | No | — | RDB$4189 | NONE/NONE |
| 14 | MM_EXT_SENSOR_ALARM | VARCHAR(1) | No | — | RDB$4190 | NONE/NONE |
| 15 | MM_IGNITION | VARCHAR(1) | No | — | RDB$4191 | NONE/NONE |
| 16 | MM_UNIT_ID | BIGINT | No | — | D_SYSID | — |
| 17 | MM_MSG_TYPE | VARCHAR(3) | No | — | RDB$4192 | NONE/NONE |
| 18 | MM_FORM_TYPE | VARCHAR(1) | No | — | RDB$4193 | NONE/NONE |
| 19 | MM_DELIVERED_DATE | TIMESTAMP | No | — | RDB$4194 | — |
| 20 | MM_OPENED_DATE | TIMESTAMP | No | — | RDB$4195 | — |
| 21 | MM_SENT_DATE | TIMESTAMP | No | — | RDB$4196 | — |
| 22 | MM_RECVEIVED_DATE | TIMESTAMP | No | — | RDB$4197 | — |
| 23 | MM_ACK_DATE | TIMESTAMP | No | — | RDB$4198 | — |
| 24 | MM_DRIVER_ID | BIGINT | No | — | D_SYSID | — |
| 25 | MM_ODOMETER | NUMERIC(9,1) | No | — | RDB$4229 | — |
| 26 | MM_POS_LATITUDE | DOUBLE PRECISION | No | — | D_LAT_LONG | — |
| 27 | MM_POS_LONGITUDE | DOUBLE PRECISION | No | — | D_LAT_LONG | — |
| 28 | MM_POS_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 29 | MM_POS_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 30 | MM_POS_DISTANCE | DOUBLE PRECISION | No | — | RDB$4230 | — |
| 31 | MM_POS_DIRECTION | VARCHAR(2) | No | — | RDB$4231 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| MM_DRIVER_KEY | MM_DRIVER_ID, MM_MSG_NUMBER | No | Yes | ASC | — |
| MM_MSG_REFERENCE_KEY | MM_MSG_REFERENCE | No | Yes | ASC | — |
| MM_PRIMARY_KEY | MM_MSG_NUMBER | Yes | Yes | ASC | — |
| MM_REVERSE_DRIVER_KEY | MM_DRIVER_ID, MM_MSG_NUMBER | No | Yes | DESC | — |
| MM_REVERSE_KEY | MM_MSG_NUMBER | No | Yes | DESC | — |
| MM_REVERSE_UNIT_KEY | MM_UNIT_ID, MM_MSG_NUMBER | No | Yes | DESC | — |
| MM_UNIT_KEY | MM_UNIT_ID, MM_MSG_NUMBER | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R150 | MC_MESSAGES.(MM_SYSTEM_TYPE, MM_FORM_NUMBER, MM_FORM_VERSION, MM_FORM_TYPE) | MC_FORMS.(MCF_SYSTEM_TYPE, MCF_NUMBER, MCF_VERSION, MCF_TYPE) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R152 | MC_QUEUE_ITEMS.(MCQI_MSG_NUMBER) | MC_MESSAGES.(MM_MSG_NUMBER) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R239 | PN_ACTIVITIES.(PNA_AURORA_MSG_ID) | MC_MESSAGES.(MM_MSG_NUMBER) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`MC_MESSAGES_AFTER_UPDATE`, `MC_MESSAGES_AI0`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
