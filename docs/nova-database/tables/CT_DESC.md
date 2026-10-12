# CT_DESC

Codes and configuration. Equipment descriptor dictionary.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTD_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTD_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | CTD_CODE | VARCHAR(4) | Yes | — | D_DESC_CODE | NONE/NONE |
| 3 | CTD_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | CTD_EMPTY_WEIGHT | NUMERIC(15,3) | No | — | RDB$6878 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTD_CODE_KEY | CTD_CODE | Yes | Yes | ASC | — |
| CTD_PRIM_KEY | CTD_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R029 | CA_AVAILABLE_EQUIP.(CAAE_EQUIP_DESC_ID) | CT_DESC.(CTD_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R099 | EQUIP_DESC.(ED_DESC_ID) | CT_DESC.(CTD_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`CT_DESC_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
