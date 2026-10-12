# GOMOTIVELANDING

Integration and reference. Gomotivelanding records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: **None declared**. No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | GML_ID | BIGINT | Yes | — | RDB$6892 | — |
| 2 | GML_AUTH_CODE | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 3 | GML_AUTH_RCVD_TIME | TIMESTAMP | No | — | RDB$6893 | — |
| 4 | GML_REQUEST_JSON | BLOB SUB_TYPE 0 | No | — | RDB$6904 | — |
| 5 | GML_TOKEN | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 6 | GML_TOKEN_EXP | INTEGER | No | — | RDB$6894 | — |
| 7 | GML_TOKEN_REAUTH | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 8 | GML_TOKEN_TYPE | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 9 | GML_TOKEN_JSON | BLOB SUB_TYPE 0 | No | — | RDB$6895 | — |
| 11 | GML_CO_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 12 | GML_CO_ADDR | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 13 | GML_CO_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 14 | GML_CO_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 15 | GML_CO_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 16 | GML_CO_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 17 | GML_CO_SCOPE | VARCHAR(1000) | No | — | RDB$6897 | NONE/NONE |
| 18 | GML_CO_RECEIVED_TIME | TIMESTAMP | No | — | RDB$6898 | — |
| 19 | GML_CO_JSON | BLOB SUB_TYPE 0 | No | — | RDB$6899 | — |
| 20 | GML_PAGE_SENT_TIME | TIMESTAMP | No | — | RDB$6900 | — |
| 21 | GML_EMAIL_ADDR | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 22 | GML_EMAIL_SENT | BLOB SUB_TYPE 0 | No | — | RDB$6901 | — |
| 23 | GML_EMAIL_SENT_TIME | TIMESTAMP | No | — | RDB$6902 | — |
| 24 | GML_AUTO_ARRIVE_CONSIGNEE | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 25 | GML_AUTO_ARRIVE_SHIPPER | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 26 | GML_AUTO_ARRIVE_STOP | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 27 | GML_AUTO_DEPART_CONSIGNEE | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 28 | GML_AUTO_DEPART_SHIPPER | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 29 | GML_AUTO_DEPART_STOP | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 30 | GML_FORMS_AVAILABLE_JSON | BLOB SUB_TYPE 0 | No | — | RDB$6903 | — |
| 31 | GML_CLIENT_ID | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 32 | GML_REDIRECT_URI | VARCHAR(150) | No | — | D_URL | NONE/NONE |
| 33 | GML_RESPONSE_TYPE | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 34 | GML_TOKEN_RCVD_TIME | BIGINT | No | — | RDB$6906 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
