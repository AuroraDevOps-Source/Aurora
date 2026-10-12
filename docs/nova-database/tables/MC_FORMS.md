# MC_FORMS

Integration and reference. Mc Forms records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MCF_SYSTEM_TYPE, MCF_NUMBER, MCF_VERSION, MCF_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MCF_SYSTEM_TYPE | VARCHAR(1) | Yes | — | RDB$4171 | NONE/NONE |
| 2 | MCF_NUMBER | BIGINT | Yes | — | RDB$4172 | — |
| 3 | MCF_VERSION | BIGINT | Yes | — | RDB$4173 | — |
| 4 | MCF_TYPE | CHAR(1) | Yes | — | RDB$4174 | NONE/NONE |
| 5 | MCF_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 6 | MCF_REPLY_FORM_NUMBER | BIGINT | No | — | RDB$4175 | — |
| 7 | MCF_DEFINITION | BLOB SUB_TYPE 1 | No | — | RDB$4176 | NONE/NONE |
| 8 | MCF_NATIVE_DEFINITION | BLOB SUB_TYPE 1 | No | — | RDB$4177 | NONE/NONE |
| 9 | MCF_REFERENCE_NUMBER | BIGINT | No | — | RDB$4214 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| MCF_PRIMARY_KEY | MCF_SYSTEM_TYPE, MCF_NUMBER, MCF_VERSION, MCF_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R150 | MC_MESSAGES.(MM_SYSTEM_TYPE, MM_FORM_NUMBER, MM_FORM_VERSION, MM_FORM_TYPE) | MC_FORMS.(MCF_SYSTEM_TYPE, MCF_NUMBER, MCF_VERSION, MCF_TYPE) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
