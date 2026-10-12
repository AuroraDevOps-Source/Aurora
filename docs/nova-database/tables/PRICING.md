# PRICING

Pricing and tariffs. Pricing records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PRMA_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PRMA_ACCOUNT | VARCHAR(12) | Yes | — | RDB$1870 | NONE/NONE |
| 2 | PRMA_EFF_DATE | DATE | No | — | RDB$1871 | — |
| 3 | PRMA_EXP_DATE | DATE | No | — | RDB$1872 | — |
| 4 | PRMA_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$1873 | — |
| 5 | PRMA_OB_PPD | NUMERIC(6,4) | No | — | RDB$1874 | — |
| 6 | PRMA_OB_COL | NUMERIC(6,4) | No | — | RDB$1875 | — |
| 7 | PRMA_OB_COL_M | NUMERIC(6,4) | No | — | RDB$1876 | — |
| 8 | PRMA_OB_PPD_M | NUMERIC(6,4) | No | — | RDB$1877 | — |
| 9 | PRMA_OB_PPD_TL | NUMERIC(6,4) | No | — | RDB$1878 | — |
| 10 | PRMA_OB_COL_TL | NUMERIC(6,4) | No | — | RDB$1879 | — |
| 11 | PRMA_OB_PPD_INTER | NUMERIC(9,2) | No | — | RDB$1880 | — |
| 12 | PRMA_OB_COL_INTER | NUMERIC(9,2) | No | — | RDB$1881 | — |
| 13 | PRMA_OB_PPD_INTRA | NUMERIC(9,2) | No | — | RDB$1882 | — |
| 14 | PRMA_OB_COL_INTRA | NUMERIC(9,2) | No | — | RDB$1883 | — |
| 15 | PRMA_IB_PPD | NUMERIC(6,4) | No | — | RDB$1884 | — |
| 16 | PRMA_IB_COL | NUMERIC(6,4) | No | — | RDB$1885 | — |
| 17 | PRMA_IB_COL_M | NUMERIC(6,4) | No | — | RDB$1886 | — |
| 18 | PRMA_IB_PPD_M | NUMERIC(6,4) | No | — | RDB$1887 | — |
| 19 | PRMA_IB_PPD_TL | NUMERIC(6,4) | No | — | RDB$1888 | — |
| 20 | PRMA_IB_COL_TL | NUMERIC(6,4) | No | — | RDB$1889 | — |
| 21 | PRMA_IB_PPD_INTER | NUMERIC(9,2) | No | — | RDB$1890 | — |
| 22 | PRMA_IB_COL_INTER | NUMERIC(9,2) | No | — | RDB$1891 | — |
| 23 | PRMA_IB_PPD_INTRA | NUMERIC(9,2) | No | — | RDB$1892 | — |
| 24 | PRMA_IB_COL_INTRA | NUMERIC(9,2) | No | — | RDB$1893 | — |
| 25 | PRMA_3_PPD | NUMERIC(6,4) | No | — | RDB$1894 | — |
| 26 | PRMA_3_COL | NUMERIC(6,4) | No | — | RDB$1895 | — |
| 27 | PRMA_3_COL_M | NUMERIC(6,4) | No | — | RDB$1896 | — |
| 28 | PRMA_3_PPD_M | NUMERIC(6,4) | No | — | RDB$1897 | — |
| 29 | PRMA_3_PPD_TL | NUMERIC(6,4) | No | — | RDB$1898 | — |
| 30 | PRMA_3_COL_TL | NUMERIC(6,4) | No | — | RDB$1899 | — |
| 31 | PRMA_3_PPD_INTER | NUMERIC(9,2) | No | — | RDB$1900 | — |
| 32 | PRMA_3_COL_INTER | NUMERIC(9,2) | No | — | RDB$1901 | — |
| 33 | PRMA_3_PPD_INTRA | NUMERIC(9,2) | No | — | RDB$1902 | — |
| 34 | PRMA_3_COL_INTRA | NUMERIC(9,2) | No | — | RDB$1903 | — |
| 35 | PRMA_INTRA_MIN | NUMERIC(9,2) | No | — | RDB$1904 | — |
| 36 | PRMA_INTER_MIN | NUMERIC(9,2) | No | — | RDB$1905 | — |
| 37 | PRMA_NO_DISC | CHAR(1) | No | — | RDB$1906 | NONE/NONE |
| 38 | PRMA_REVIEW | DATE | No | — | RDB$1907 | — |
| 39 | PRMA_COD | CHAR(1) | No | — | RDB$1908 | NONE/NONE |
| 40 | PRMA_T_EFF_DATE | DATE | No | — | RDB$1909 | — |
| 41 | PRMA_T_EXP_DATE | DATE | No | — | RDB$1910 | — |
| 42 | PRMA_DATE_OB_IB | CHAR(1) | No | — | RDB$1911 | NONE/NONE |
| 43 | PRMA_PR_SYS_WIDE | CHAR(1) | No | — | RDB$1912 | NONE/NONE |
| 44 | PRMA_OR_VOL_DISC | CHAR(1) | No | — | RDB$1913 | NONE/NONE |
| 45 | PRMA_APP_METHOD | CHAR(1) | No | — | RDB$1914 | NONE/NONE |
| 46 | PRMA_PLAN | VARCHAR(12) | No | — | RDB$1915 | NONE/NONE |
| 47 | PRMA_EXP_RULE | CHAR(1) | No | — | RDB$1916 | NONE/NONE |
| 48 | PRMA_MIN_ON_MAN | CHAR(1) | No | — | RDB$1917 | NONE/NONE |
| 49 | PRMA_3RD_F_T | CHAR(1) | No | — | RDB$1918 | NONE/NONE |
| 50 | PRMA_WGT_BRK_RULE | CHAR(1) | No | — | RDB$1919 | NONE/NONE |
| 51 | PRMA_MAX_DISC_AMT | NUMERIC(9,2) | No | — | RDB$1920 | — |
| 52 | PRMA_MAX_DISC_PERC | NUMERIC(6,4) | No | — | RDB$1921 | — |
| 53 | PRMA_MIN_SHIP_WGT | NUMERIC(9,3) | No | — | RDB$1922 | — |
| 54 | PRMA_LCHG_DATE | DATE | No | — | RDB$1923 | — |
| 55 | PRMA_LCHG_INIT | VARCHAR(8) | No | — | RDB$1924 | NONE/NONE |
| 56 | PRMA_TL_WGT | NUMERIC(9,3) | No | — | RDB$1925 | — |
| 57 | PRMA_DRS | CHAR(1) | No | — | RDB$1926 | NONE/NONE |
| 58 | PRMA_DISC_RM | CHAR(1) | No | — | RDB$1927 | NONE/NONE |
| 59 | PRMA_FIN_CHRG | CHAR(1) | No | — | RDB$1928 | NONE/NONE |
| 60 | PRMA_BLIND_DR | CHAR(1) | No | — | RDB$1929 | NONE/NONE |
| 61 | PRMA_LA_OUT | NUMERIC(9,2) | No | — | RDB$1930 | — |
| 62 | PRMA_LA_OB_CMPT | CHAR(1) | No | — | RDB$1931 | NONE/NONE |
| 63 | PRMA_LA_IN | NUMERIC(9,2) | No | — | RDB$1932 | — |
| 64 | PRMA_LA_IN_CMPT | CHAR(1) | No | — | RDB$1933 | NONE/NONE |
| 65 | PRMA_LA_IL | NUMERIC(9,2) | No | — | RDB$1934 | — |
| 66 | PRMA_LA_IL_CMPT | CHAR(1) | No | — | RDB$1935 | NONE/NONE |
| 67 | PRMA_LA_MIN | CHAR(1) | No | — | RDB$1936 | NONE/NONE |
| 68 | PRMA_LA_TL | CHAR(1) | No | — | RDB$1937 | NONE/NONE |
| 69 | PRMA_LA_MAX | NUMERIC(9,2) | No | — | RDB$1938 | — |
| 70 | PRMA_LA_GRP_ID | VARCHAR(12) | No | — | RDB$1939 | NONE/NONE |
| 71 | PRMA_LA_OB_PAY | CHAR(1) | No | — | RDB$1940 | NONE/NONE |
| 72 | PRMA_LA_IB_PAY | CHAR(1) | No | — | RDB$1941 | NONE/NONE |
| 73 | PRMA_DIM_RATE | INTEGER | No | — | RDB$6369 | — |
| 74 | PRMA_MILE_TYPE | VARCHAR(1) | No | — | RDB$7232 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PRMA_CUST | PRMA_ACCOUNT, PRMA_EFF_DATE, PRMA_EXP_DATE | No | Yes | ASC | — |
| PRMA_KEY | PRMA_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R249 | PRICE_MAX_CHGS.(PRMC_REC_NUM) | PRICING.(PRMA_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R254 | PRICE_WGT_BRKS.(PRWB_REC_NUM) | PRICING.(PRMA_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R255 | PRICING.(PRMA_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
