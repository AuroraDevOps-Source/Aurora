# CA_INSURANCE

Accounts and carriers. Ca Insurance records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CAI_OWNER_TYPE, CAI_OWNER_ID, CAI_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CAI_OWNER_TYPE | VARCHAR(2) | Yes | — | RDB$2478 | NONE/NONE |
| 2 | CAI_OWNER_ID | VARCHAR(18) | Yes | — | RDB$2479 | NONE/NONE |
| 3 | CAI_TYPE | INTEGER | Yes | — | RDB$2480 | — |
| 4 | CAI_INS_COMPANY | VARCHAR(30) | No | — | RDB$2481 | NONE/NONE |
| 5 | CAI_POLICY_NUMBER | VARCHAR(24) | No | — | RDB$2482 | NONE/NONE |
| 6 | CAI_EXPIRY_DATE | TIMESTAMP | No | — | RDB$2483 | — |
| 7 | CAI_AMOUNT | NUMERIC(15,2) | No | — | RDB$2484 | — |
| 8 | CAI_LIMIT | NUMERIC(15,2) | No | — | RDB$2485 | — |
| 9 | CAI_AGENT | VARCHAR(40) | No | — | RDB$2486 | NONE/NONE |
| 10 | CAI_AGENT_ADDR1 | VARCHAR(50) | No | — | RDB$2487 | NONE/NONE |
| 11 | CAI_AGENT_ADDR2 | VARCHAR(50) | No | — | RDB$2488 | NONE/NONE |
| 12 | CAI_AGENT_CITY | VARCHAR(35) | No | — | RDB$2489 | NONE/NONE |
| 13 | CAI_AGENT_STATE | VARCHAR(2) | No | — | RDB$2490 | NONE/NONE |
| 14 | CAI_AGENT_ZIP | VARCHAR(9) | No | — | RDB$2491 | NONE/NONE |
| 15 | CAI_AGENT_COUNTRY | VARCHAR(4) | No | — | RDB$2492 | NONE/NONE |
| 16 | CAI_AGENT_PHONE | VARCHAR(10) | No | — | RDB$2493 | NONE/NONE |
| 17 | CAI_AGENT_EXTN | VARCHAR(5) | No | — | RDB$2494 | NONE/NONE |
| 18 | CAI_AGENT_FAX | VARCHAR(10) | No | — | RDB$2495 | NONE/NONE |
| 19 | CAI_AGENT_EMAIL | VARCHAR(150) | No | — | RDB$2496 | NONE/NONE |
| 20 | CAI_EFF_DATE | TIMESTAMP | No | — | RDB$2497 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CA_INSURANCE_PRIMARY | CAI_OWNER_TYPE, CAI_OWNER_ID, CAI_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R034 | CA_INSURANCE.(CAI_TYPE) | CT_INS_TYPES.(CTI_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
