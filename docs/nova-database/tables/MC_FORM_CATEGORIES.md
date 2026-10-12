# MC_FORM_CATEGORIES

Integration and reference. Mc Form Categories records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MFC_SYSTEM_TYPE, MFC_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MFC_SYSTEM_TYPE | VARCHAR(1) | Yes | — | RDB$5504 | NONE/NONE |
| 2 | MFC_ID | VARCHAR(50) | Yes | — | RDB$5505 | NONE/NONE |
| 3 | MFC_NAME | VARCHAR(50) | No | — | RDB$5506 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_MC_FORM_CATEGORIES | MFC_SYSTEM_TYPE, MFC_ID | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
