# OPS_MANIFEST_STOPS

Orders and manifests. Sequence bridge from a manifest routing stop to an order pickup/delivery stop.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OSM_STOP_ID, OSM_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OSM_STOP_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | OSM_SEQUENCE | INTEGER | Yes | — | RDB$694 | — |
| 3 | OSM_PKUP_DELV_ID | BIGINT | Yes | — | D_SYSID | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| OSM_CHILD_STOP_INDEX | OSM_PKUP_DELV_ID | No | Yes | ASC | — |
| PK_OPS_MANIFEST_STOPS | OSM_STOP_ID, OSM_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R206 | OPS_MANIFEST_STOPS.(OSM_PKUP_DELV_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R207 | OPS_MANIFEST_STOPS.(OSM_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping; Trigger OPS_STOP_REC_AFTER_DELETE explicitly deletes matching child rows  |
| R233 | OPS_STOP_TLR_SPACES.(OSB_STOP_ID) | OPS_MANIFEST_STOPS.(OSM_PKUP_DELV_ID) | High | Trigger OPS_MFST_STOP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

`OPS_MANIFEST_STOPS_AFTER_MOD`, `OPS_MFST_STOP_AFTER_DELETE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
