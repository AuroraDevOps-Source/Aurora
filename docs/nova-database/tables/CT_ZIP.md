# CT_ZIP

Locations and routing. Ct Zip records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTZ_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTZ_ID | BIGINT | Yes | — | RDB$50 | — |
| 2 | CTZ_ZIP | VARCHAR(9) | Yes | — | D_ZIP | NONE/NONE |
| 3 | CTZ_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 4 | CTZ_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 5 | CTZ_LATITUDE | NUMERIC(6,4) | No | — | RDB$51 | — |
| 6 | CTZ_LONGITUDE | NUMERIC(7,4) | No | — | RDB$52 | — |
| 7 | CTZ_IMPORTED | VARCHAR(1) | No | — | RDB$53 | NONE/NONE |
| 8 | CTZ_ROUTE | VARCHAR(12) | No | — | D_ROUTE_ID | NONE/NONE |
| 9 | CTZ_COUNTY | VARCHAR(35) | No | — | RDB$717 | NONE/NONE |
| 10 | CTZ_COMMERCIAL_ZONE | VARCHAR(6) | No | — | RDB$5550 | NONE/NONE |
| 11 | CTZ_UTC_OFFSET | INTEGER | No | — | RDB$5553 | — |
| 12 | CTZ_DST_OFFSET | INTEGER | No | — | RDB$5554 | — |
| 13 | CTZ_TERM | VARCHAR(12) | No | — | RDB$5646 | NONE/NONE |
| 14 | CTZ_SERV_AREA | VARCHAR(12) | No | — | RDB$5647 | NONE/NONE |
| 15 | CTZ_SERV_DAYS | SMALLINT | No | — | RDB$5648 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CT_ZIP_CITY_KEY | CTZ_CITY, CTZ_STATE | No | Yes | ASC | — |
| CT_ZIP_KEY | CTZ_ZIP | No | Yes | ASC | — |
| CT_ZIP_LAT_LONG_KEY | CTZ_LATITUDE, CTZ_LONGITUDE | No | Yes | ASC | — |
| CT_ZIP_ROUTE_KEY | CTZ_ROUTE, CTZ_ZIP | No | Yes | ASC | — |
| PK_CT_ZIP | CTZ_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R059 | CT_ZIP.(CTZ_ROUTE) | CT_ROUTES.(CTR_ROUTE_ID) | High | CT_ROUTES_AD0 clears CTZ_ROUTE where it equals OLD.CTR_ROUTE_ID  |

## Trigger evidence

`CT_ZIP_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
