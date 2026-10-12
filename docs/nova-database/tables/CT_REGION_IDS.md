# CT_REGION_IDS

Locations and routing. Ct Region Ids records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTR_REGION_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTR_REGION_ID | VARCHAR(4) | Yes | — | D_REGION_ID | NONE/NONE |
| 2 | CTR_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTR_PRIM_KEY | CTR_REGION_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R004 | ACCOUNT.(AC_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R051 | CT_REGION_RANGES.(CTRR_REGION_ID) | CT_REGION_IDS.(CTR_REGION_ID) | High | Trigger CTR_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R052 | CT_REGION_USER_DEFS.(CTRU_REGION_ID) | CT_REGION_IDS.(CTR_REGION_ID) | High | Trigger CTR_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R106 | EQUIP_HEADER.(EH_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R148 | LOCMAST.(LM_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R190 | OPS_HEADER.(PB_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R226 | OPS_STOP_REC.(OPS_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`CTR_AFTER_DELETE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
