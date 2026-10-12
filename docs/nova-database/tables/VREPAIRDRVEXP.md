# VREPAIRDRVEXP

Maintenance and purchasing. Vrepairdrvexp records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VRDE_RN_PREF, VRDE_RN_NUM, VRDE_RN_SUF, VRDE_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VRDE_RN_PREF | VARCHAR(1) | Yes | — | RDB$6487 | NONE/NONE |
| 2 | VRDE_RN_NUM | INTEGER | Yes | — | RDB$6488 | — |
| 3 | VRDE_RN_SUF | VARCHAR(2) | Yes | — | RDB$6489 | NONE/NONE |
| 4 | VRDE_SEQ | INTEGER | Yes | — | RDB$6490 | — |
| 5 | VRDE_DS_TYPE | VARCHAR(4) | No | — | RDB$6491 | NONE/NONE |
| 6 | VRDE_DED_AMT | NUMERIC(16,2) | No | — | RDB$6492 | — |
| 7 | VRDE_FREQ | VARCHAR(2) | No | — | RDB$6493 | NONE/NONE |
| 8 | VRDE_FREQ_AMT | NUMERIC(16,2) | No | — | RDB$6494 | — |
| 9 | VRDE_STATUS | VARCHAR(2) | No | — | RDB$6495 | NONE/NONE |
| 10 | VRDE_LAST_DT | DATE | No | — | RDB$6496 | — |
| 11 | VRDE_TAKEN | NUMERIC(16,2) | No | — | RDB$6497 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VRDE_KEY | VRDE_RN_PREF, VRDE_RN_NUM, VRDE_RN_SUF, VRDE_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R282 | VREPAIRDRVEXP.(VRDE_RN_PREF, VRDE_RN_NUM, VRDE_RN_SUF) | VREPAIRHDR.(VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
