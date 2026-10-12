# CT_SERV_AREAS

Locations and routing. Ct Serv Areas records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTSA_AREA_ID, CTSA_LOCATION). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTSA_AREA_ID | VARCHAR(12) | Yes | — | D_ROUTE_ID | NONE/NONE |
| 2 | CTSA_LOCATION | VARCHAR(12) | Yes | — | D_LOCATION | NONE/NONE |
| 3 | CTSA_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | CTSA_POLYGON | BLOB SUB_TYPE 0 | No | — | RDB$5644 | — |
| 5 | CTSA_SERVICE_DAYS | SMALLINT | No | — | RDB$5645 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_SERV_AREAS | CTSA_AREA_ID, CTSA_LOCATION | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R054 | CT_SERV_AREAS.(CTSA_LOCATION) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
