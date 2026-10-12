# WEB_DASHBOARD_ITEMS

Users and access. Web Dashboard Items records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (WDI_USER_ID, WDI_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | WDI_USER_ID | VARCHAR(50) | Yes | — | RDB$5018 | NONE/NONE |
| 2 | WDI_SEQUENCE | INTEGER | Yes | — | RDB$5019 | — |
| 3 | WDI_ITEM | VARCHAR(10) | No | — | RDB$5020 | NONE/NONE |
| 4 | WDI_OPTIONS | VARCHAR(255) | No | — | RDB$5082 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_WEB_DASHBOARD_ITEMS | WDI_USER_ID, WDI_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R295 | WEB_DASHBOARD_ITEMS.(WDI_USER_ID) | WEB_USERS.(WU_USER_ID) | High | Trigger WEB_USER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
