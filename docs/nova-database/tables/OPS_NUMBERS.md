# OPS_NUMBERS

Orders and manifests. Order/stop reference numbers with type and qualifier.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OPN_ORDER_ID, OPN_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OPN_ORDER_ID | NUMERIC(9,0) | Yes | — | RDB$559 | — |
| 2 | OPN_SEQUENCE | BIGINT | Yes | — | RDB$560 | — |
| 3 | OPN_STOP_ID | BIGINT | Yes | — | D_SYSID | — |
| 4 | OPN_TYPE | VARCHAR(4) | Yes | — | RDB$561 | NONE/NONE |
| 5 | OPN_QUALIFIER | VARCHAR(4) | No | — | D_QUALIFIER | NONE/NONE |
| 6 | OPN_NUMBER | VARCHAR(80) | Yes | — | D_CONTROL_NUMBER | NONE/NONE |
| 7 | OPN_PIECES | NUMERIC(15,3) | No | — | RDB$6105 | — |
| 8 | OPN_WEIGHT | NUMERIC(15,3) | No | — | RDB$6106 | — |
| 9 | OPN_TERMINAL | VARCHAR(12) | No | — | RDB$6107 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| OPS_NUMBER_KEY | OPN_NUMBER, OPN_TYPE | No | Yes | ASC | — |
| PK_OPS_NUMBERS | OPN_ORDER_ID, OPN_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R209 | OPS_NUMBERS.(OPN_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows; Column roles and key structure; inferred, not data-validated. Delete cleanup is not insert/update referential validation. |
| R210 | OPS_NUMBERS.(OPN_QUALIFIER) | QUAL128.(Q_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R211 | OPS_NUMBERS.(OPN_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R212 | OPS_NUMBERS.(OPN_TERMINAL) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R213 | OPS_NUMBERS.(OPN_TYPE) | CT_NUMBER_TYPES.(CTN_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`OPS_NUMBERS_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
