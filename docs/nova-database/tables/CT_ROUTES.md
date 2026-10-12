# CT_ROUTES

Locations and routing. Ct Routes records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTR_ROUTE_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTR_ROUTE_ID | VARCHAR(12) | Yes | — | D_ROUTE_ID | NONE/NONE |
| 2 | CTR_LOCATION | VARCHAR(12) | Yes | — | D_LOCATION | NONE/NONE |
| 3 | CTR_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | CTR_POLYGON | BLOB SUB_TYPE 0 | No | — | RDB$1513 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CT_ROUTES_TERMINAL_KEY | CTR_LOCATION, CTR_ROUTE_ID | No | Yes | ASC | — |
| PK_CT_ROUTES | CTR_ROUTE_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R006 | ACCOUNT.(AC_ROUTE_ID) | CT_ROUTES.(CTR_ROUTE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R053 | CT_ROUTES.(CTR_LOCATION) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R059 | CT_ZIP.(CTZ_ROUTE) | CT_ROUTES.(CTR_ROUTE_ID) | High | CT_ROUTES_AD0 clears CTZ_ROUTE where it equals OLD.CTR_ROUTE_ID  |
| R181 | OPS_HEADER.(PB_DEL_ROUTE) | CT_ROUTES.(CTR_ROUTE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R189 | OPS_HEADER.(PB_PU_ROUTE) | CT_ROUTES.(CTR_ROUTE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R227 | OPS_STOP_REC.(OPS_ROUTE) | CT_ROUTES.(CTR_ROUTE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`CT_ROUTES_AD0`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
