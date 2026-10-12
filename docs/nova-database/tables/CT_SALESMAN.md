# CT_SALESMAN

Codes and configuration. Ct Salesman records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTSM_SALESMAN_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTSM_SALESMAN_ID | VARCHAR(12) | Yes | — | RDB$6121 | NONE/NONE |
| 2 | CTSM_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 3 | CTSM_ADDR1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 4 | CTSM_ADDR2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 5 | CTSM_ADDR3 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 6 | CTSM_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 7 | CTSM_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 8 | CTSM_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 9 | CTSM_COUNTRY_CD | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 10 | CTSM_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 11 | CTSM_CONTACT | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 12 | CTSM_CC | VARCHAR(2) | No | — | RDB$6122 | NONE/NONE |
| 13 | CTSM_COMM_RATE | NUMERIC(7,4) | No | — | RDB$6123 | — |
| 14 | CTSM_COMM_LIMIT | NUMERIC(7,2) | No | — | RDB$6124 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CT_SALESMAN_KEY | CTSM_SALESMAN_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R007 | ACCOUNT.(AC_SALESMAN_ID) | CT_SALESMAN.(CTSM_SALESMAN_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R175 | OPS_HEADER.(PBSALMAN) | CT_SALESMAN.(CTSM_SALESMAN_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
