# OPS_STOP_TLR_SPACES

Orders and manifests. Ops Stop Tlr Spaces records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OSB_STOP_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OSB_STOP_ID | BIGINT | Yes | — | RDB$7016 | — |
| 2 | OSB_ORDER_ID | BIGINT | Yes | — | RDB$7017 | — |
| 3 | OSB_TLR_ID | INTEGER | No | — | RDB$7018 | — |
| 4 | OSB_MFST_ID | BIGINT | No | — | RDB$7019 | — |
| 5 | OSB_BIN1_CUFT | INTEGER | No | — | RDB$7020 | — |
| 6 | OSB_BIN2_CUFT | INTEGER | No | — | RDB$7021 | — |
| 7 | OSB_BIN3_CUFT | INTEGER | No | — | RDB$7022 | — |
| 8 | OSB_BIN4_CUFT | INTEGER | No | — | RDB$7023 | — |
| 9 | OSB_BIN5_CUFT | INTEGER | No | — | RDB$7024 | — |
| 10 | OSB_BIN6_CUFT | INTEGER | No | — | RDB$7025 | — |
| 11 | OSB_BIN7_CUFT | INTEGER | No | — | RDB$7026 | — |
| 12 | OSB_BIN8_CUFT | INTEGER | No | — | RDB$7027 | — |
| 13 | OSB_BIN9_CUFT | INTEGER | No | — | RDB$7028 | — |
| 14 | OSB_BIN10_CUFT | INTEGER | No | — | RDB$7029 | — |
| 15 | OSB_BIN11_CUFT | INTEGER | No | — | RDB$7030 | — |
| 16 | OSB_BIN12_CUFT | INTEGER | No | — | RDB$7031 | — |
| 17 | OSB_BIN1_WGT | NUMERIC(15,3) | No | — | RDB$7032 | — |
| 18 | OSB_BIN2_WGT | NUMERIC(15,3) | No | — | RDB$7033 | — |
| 19 | OSB_BIN3_WGT | NUMERIC(15,3) | No | — | RDB$7034 | — |
| 20 | OSB_BIN4_WGT | NUMERIC(15,3) | No | — | RDB$7035 | — |
| 21 | OSB_BIN5_WGT | NUMERIC(15,3) | No | — | RDB$7036 | — |
| 22 | OSB_BIN6_WGT | NUMERIC(15,3) | No | — | RDB$7037 | — |
| 23 | OSB_BIN7_WGT | NUMERIC(15,3) | No | — | RDB$7038 | — |
| 24 | OSB_BIN8_WGT | NUMERIC(15,3) | No | — | RDB$7039 | — |
| 25 | OSB_BIN9_WGT | NUMERIC(15,3) | No | — | RDB$7040 | — |
| 26 | OSB_BIN10_WGT | NUMERIC(15,3) | No | — | RDB$7041 | — |
| 27 | OSB_BIN11_WGT | NUMERIC(15,3) | No | — | RDB$7042 | — |
| 28 | OSB_BIN12_WGT | NUMERIC(15,3) | No | — | RDB$7043 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IDX_OPS_STOP_TLR_ORDERNUM_KEY | OSB_ORDER_ID | No | Yes | ASC | — |
| PK_OPS_STOP_TLR_SPACES | OSB_STOP_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R231 | OPS_STOP_TLR_SPACES.(OSB_MFST_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R232 | OPS_STOP_TLR_SPACES.(OSB_ORDER_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R233 | OPS_STOP_TLR_SPACES.(OSB_STOP_ID) | OPS_MANIFEST_STOPS.(OSM_PKUP_DELV_ID) | High | Trigger OPS_MFST_STOP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Referenced fields have no exact active unique index; multiple matches are possible. |
| R234 | OPS_STOP_TLR_SPACES.(OSB_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R235 | OPS_STOP_TLR_SPACES.(OSB_TLR_ID) | EQUIP_HEADER.(EH_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

`OPS_STOP_TLR_SPACES_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
