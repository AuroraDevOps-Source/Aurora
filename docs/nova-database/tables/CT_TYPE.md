# CT_TYPE

Codes and configuration. Equipment type dictionary.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTT_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTT_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | CTT_CODE | VARCHAR(4) | Yes | — | D_EQ_TYPE | NONE/NONE |
| 3 | CTT_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | CTT_EMPTY_WEIGHT | NUMERIC(15,3) | No | — | RDB$6877 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTT_CODE_KEY | CTT_CODE | Yes | Yes | ASC | — |
| CTT_PRIM_KEY | CTT_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R108 | EQUIP_HEADER.(EH_UNIT_TYPE) | CT_TYPE.(CTT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R132 | FUEL.(FP_EQUIP_TYPE) | CT_TYPE.(CTT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R135 | FUEL_CARD.(FC_UNIT_TYPE) | CT_TYPE.(CTT_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R285 | VREPAIRHDR.(VRH_UNIT_TYPE_CTT_ID) | CT_TYPE.(CTT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`CT_TYPE_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
