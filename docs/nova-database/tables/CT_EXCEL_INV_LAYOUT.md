# CT_EXCEL_INV_LAYOUT

Codes and configuration. Ct Excel Inv Layout records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CEIL_SS_ID, CEIL_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CEIL_SS_ID | VARCHAR(36) | Yes | — | RDB$7196 | NONE/NONE |
| 2 | CEIL_SEQ | INTEGER | Yes | — | RDB$7197 | — |
| 3 | CEIL_DESC | VARCHAR(128) | No | — | RDB$7198 | NONE/NONE |
| 4 | CEIL_TYPE | VARCHAR(1) | No | — | RDB$7199 | NONE/NONE |
| 5 | CEIL_DFLT_FONT | VARCHAR(36) | No | — | RDB$7200 | NONE/NONE |
| 6 | CEIL_START_COL | INTEGER | No | — | RDB$7201 | — |
| 7 | CEIL_END_COL | INTEGER | No | — | RDB$7202 | — |
| 8 | CEIL_START_ROW | INTEGER | No | — | RDB$7203 | — |
| 9 | CEIL_END_ROW | INTEGER | No | — | RDB$7204 | — |
| 10 | CEIL_FIELD | VARCHAR(60) | No | — | RDB$7205 | NONE/NONE |
| 11 | CEIL_TEXT | VARCHAR(512) | No | — | RDB$7206 | NONE/NONE |
| 12 | CEIL_FONT | VARCHAR(36) | No | — | RDB$7207 | NONE/NONE |
| 13 | CEIL_WIDTH_PX | INTEGER | No | — | RDB$7208 | — |
| 14 | CEIL_FORMAT | VARCHAR(36) | No | — | RDB$7211 | NONE/NONE |
| 15 | CEIL_ALIGN | VARCHAR(2) | No | — | RDB$7212 | NONE/NONE |
| 16 | CEIL_LOGO_PATH | VARCHAR(128) | No | — | RDB$7213 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_EXCEL_INV_LAYOUT | CEIL_SS_ID, CEIL_SEQ | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
