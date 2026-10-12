# CA_EQUIP_INVENTORY

Accounts and carriers. Ca Equip Inventory records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CAEI_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CAEI_ID | INTEGER | Yes | — | RDB$5414 | — |
| 3 | CAEI_AVAIL_DATE_LOW | DATE | No | — | RDB$5416 | — |
| 4 | CAEI_AVAIL_DATE_HIGH | DATE | No | — | RDB$5417 | — |
| 5 | CAEI_EQUIP_TYPE | INTEGER | No | — | RDB$5418 | — |
| 6 | CAEI_EQUIP_DESC1 | INTEGER | No | — | RDB$5419 | — |
| 7 | CAEI_EQUIP_DESC2 | INTEGER | No | — | RDB$5420 | — |
| 8 | CAEI_EQUIP_DESC3 | INTEGER | No | — | RDB$5421 | — |
| 9 | CAEI_LOC_TYPE | CHAR(1) | No | — | RDB$5422 | NONE/NONE |
| 10 | CAEI_LOCATION | VARCHAR(8) | No | — | RDB$5423 | NONE/NONE |
| 11 | CAEI_LOC_CITY | VARCHAR(22) | No | — | RDB$5424 | NONE/NONE |
| 12 | CAEI_LOC_STATE | CHAR(2) | No | — | RDB$5425 | NONE/NONE |
| 13 | CAEI_LOC_ZIP | VARCHAR(9) | No | — | RDB$5426 | NONE/NONE |
| 14 | CAEI_LOC_LONG | VARCHAR(50) | No | — | RDB$5427 | NONE/NONE |
| 15 | CAEI_NUM_AVAILABLE | INTEGER | No | — | RDB$5428 | — |
| 16 | CAEI_ACCT_ID | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CAEI_ACCT_KEY | CAEI_ACCT_ID, CAEI_AVAIL_DATE_LOW | No | Yes | ASC | — |
| CAEI_CITY_KEY | CAEI_LOC_CITY, CAEI_LOC_STATE, CAEI_AVAIL_DATE_LOW | No | Yes | ASC | — |
| CAEI_DATE_KEY | CAEI_AVAIL_DATE_LOW | No | Yes | ASC | — |
| CAEI_EQUIP_TYPE_KEY | CAEI_EQUIP_TYPE, CAEI_AVAIL_DATE_LOW | No | Yes | ASC | — |
| CAEI_ZIP_KEY | CAEI_LOC_ZIP, CAEI_AVAIL_DATE_LOW | No | Yes | ASC | — |
| PK_CA_EQUIP_INVENTORY | CAEI_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R030 | CA_EQUIP_INVENTORY.(CAEI_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`CAEI_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
