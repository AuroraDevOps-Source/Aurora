# NOTIFY_TEXT_CARRIER

Codes and configuration. Notify Text Carrier records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (NTC_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | NTC_ID | BIGINT | Yes | — | RDB$3240 | — |
| 2 | NTC_CARRIER | VARCHAR(40) | Yes | — | RDB$3241 | NONE/NONE |
| 3 | NTC_EMAIL | VARCHAR(150) | Yes | — | RDB$3242 | NONE/NONE |
| 4 | NTC_MAX_LENGTH | INTEGER | No | — | RDB$3243 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| NTC_KEY | NTC_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R018 | AC_CONTACT.(ACC_NOTIFY_TEXT_CARRIER) | NOTIFY_TEXT_CARRIER.(NTC_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R065 | DRIVER_HEADER.(DH_CELL_TEXT_CARRIER) | NOTIFY_TEXT_CARRIER.(NTC_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R163 | OPS_AGENTS.(OPA_DRIVER_CELL_CARRIER) | NOTIFY_TEXT_CARRIER.(NTC_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
