# IM_CONT_CHRG_MX

Pricing and tariffs. Im Cont Chrg Mx records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (IMCX_PD_OR_STOR, IMCX_SCAC, IMCX_EQ_TYP, IMCX_DESCID_1, IMCX_DESCID_2, IMCX_DESCID_3, IMCX_SHIP_ID, IMCX_CONS_ID, IMCX_BILLTO_ID, IMCX_WORK_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | IMCX_PD_OR_STOR | VARCHAR(1) | Yes | — | RDB$3766 | NONE/NONE |
| 2 | IMCX_SCAC | VARCHAR(12) | Yes | — | RDB$3767 | NONE/NONE |
| 3 | IMCX_EQ_TYP | BIGINT | Yes | — | RDB$3768 | — |
| 4 | IMCX_DESCID_1 | BIGINT | Yes | — | RDB$3769 | — |
| 5 | IMCX_DESCID_2 | BIGINT | Yes | — | RDB$3770 | — |
| 6 | IMCX_DESCID_3 | BIGINT | Yes | — | RDB$3771 | — |
| 7 | IMCX_SHIP_ID | VARCHAR(12) | Yes | — | RDB$3772 | NONE/NONE |
| 8 | IMCX_CONS_ID | VARCHAR(12) | Yes | — | RDB$3773 | NONE/NONE |
| 9 | IMCX_BILLTO_ID | VARCHAR(12) | Yes | — | RDB$3774 | NONE/NONE |
| 10 | IMCX_WORK_CODE | VARCHAR(2) | Yes | — | RDB$3775 | NONE/NONE |
| 11 | IMCX_BEG_STAT_1 | BIGINT | No | — | RDB$3776 | — |
| 12 | IMCX_BEG_STAT_2 | BIGINT | No | — | RDB$3777 | — |
| 13 | IMCX_BEG_STAT_3 | BIGINT | No | — | RDB$3778 | — |
| 14 | IMCX_BEG_STAT_4 | BIGINT | No | — | RDB$3779 | — |
| 15 | IMCX_BEG_STAT_5 | BIGINT | No | — | RDB$3780 | — |
| 16 | IMCX_BEG_STAT_6 | BIGINT | No | — | RDB$3781 | — |
| 17 | IMCX_END_STAT_1 | BIGINT | No | — | RDB$3782 | — |
| 18 | IMCX_END_STAT_2 | BIGINT | No | — | RDB$3783 | — |
| 19 | IMCX_END_STAT_3 | BIGINT | No | — | RDB$3784 | — |
| 20 | IMCX_END_STAT_4 | BIGINT | No | — | RDB$3785 | — |
| 21 | IMCX_END_STAT_5 | BIGINT | No | — | RDB$3786 | — |
| 22 | IMCX_END_STAT_6 | BIGINT | No | — | RDB$3787 | — |
| 23 | IMCX_CUTOFF_TIME | TIME | No | — | RDB$3788 | — |
| 24 | IMCX_CT_DAYS_1 | INTEGER | No | — | RDB$3789 | — |
| 25 | IMCX_CT_RATE_1 | NUMERIC(14,4) | No | — | RDB$3790 | — |
| 26 | IMCX_CT_CHRG_WKEND_1 | VARCHAR(1) | No | — | RDB$3791 | NONE/NONE |
| 27 | IMCX_CT_CHRG_HOL_1 | VARCHAR(1) | No | — | RDB$3792 | NONE/NONE |
| 28 | IMCX_CT_INC_WKEND_1 | VARCHAR(1) | No | — | RDB$3793 | NONE/NONE |
| 29 | IMCX_CT_INC_HOL_1 | VARCHAR(1) | No | — | RDB$3794 | NONE/NONE |
| 30 | IMCX_CT_DAYS_2 | INTEGER | No | — | RDB$3795 | — |
| 31 | IMCX_CT_RATE_2 | NUMERIC(14,4) | No | — | RDB$3796 | — |
| 32 | IMCX_CT_CHRG_WKEND_2 | VARCHAR(1) | No | — | RDB$3797 | NONE/NONE |
| 33 | IMCX_CT_CHRG_HOL_2 | VARCHAR(1) | No | — | RDB$3798 | NONE/NONE |
| 34 | IMCX_CT_INC_WKEND_2 | VARCHAR(1) | No | — | RDB$3799 | NONE/NONE |
| 35 | IMCX_CT_INC_HOL_2 | VARCHAR(1) | No | — | RDB$3800 | NONE/NONE |
| 36 | IMCX_CT_DAYS_3 | INTEGER | No | — | RDB$3801 | — |
| 37 | IMCX_CT_RATE_3 | NUMERIC(14,4) | No | — | RDB$3802 | — |
| 38 | IMCX_CT_CHRG_WKEND_3 | VARCHAR(1) | No | — | RDB$3803 | NONE/NONE |
| 39 | IMCX_CT_CHRG_HOL_3 | VARCHAR(1) | No | — | RDB$3804 | NONE/NONE |
| 40 | IMCX_CT_INC_WKEND_3 | VARCHAR(1) | No | — | RDB$3805 | NONE/NONE |
| 41 | IMCX_CT_INC_HOL_3 | VARCHAR(1) | No | — | RDB$3806 | NONE/NONE |
| 42 | IMCX_CT_DAYS_4 | INTEGER | No | — | RDB$3807 | — |
| 43 | IMCX_CT_RATE_4 | NUMERIC(14,4) | No | — | RDB$3808 | — |
| 44 | IMCX_CT_CHRG_WKEND_4 | VARCHAR(1) | No | — | RDB$3809 | NONE/NONE |
| 45 | IMCX_CT_CHRG_HOL_4 | VARCHAR(1) | No | — | RDB$3810 | NONE/NONE |
| 46 | IMCX_CT_INC_WKEND_4 | VARCHAR(1) | No | — | RDB$3811 | NONE/NONE |
| 47 | IMCX_CT_INC_HOL_4 | VARCHAR(1) | No | — | RDB$3812 | NONE/NONE |
| 48 | IMCX_CT_DAYS_5 | INTEGER | No | — | RDB$3813 | — |
| 49 | IMCX_CT_RATE_5 | NUMERIC(14,4) | No | — | RDB$3814 | — |
| 50 | IMCX_CT_CHRG_WKEND_5 | VARCHAR(1) | No | — | RDB$3815 | NONE/NONE |
| 51 | IMCX_CT_CHRG_HOL_5 | VARCHAR(1) | No | — | RDB$3816 | NONE/NONE |
| 52 | IMCX_CT_INC_WKEND_5 | VARCHAR(1) | No | — | RDB$3817 | NONE/NONE |
| 53 | IMCX_CT_INC_HOL_5 | VARCHAR(1) | No | — | RDB$3818 | NONE/NONE |
| 54 | IMCX_HOLIDAY_CODE_1 | VARCHAR(6) | No | — | RDB$3819 | NONE/NONE |
| 55 | IMCX_HOLIDAY_CODE_2 | VARCHAR(6) | No | — | RDB$3820 | NONE/NONE |
| 56 | IMCX_HOLIDAY_CODE_3 | VARCHAR(6) | No | — | RDB$3821 | NONE/NONE |
| 57 | IMCX_HOLIDAY_CODE_4 | VARCHAR(6) | No | — | RDB$3822 | NONE/NONE |
| 58 | IMCX_HOLIDAY_CODE_5 | VARCHAR(6) | No | — | RDB$3823 | NONE/NONE |
| 59 | IMCX_HOLIDAY_CODE_6 | VARCHAR(6) | No | — | RDB$3824 | NONE/NONE |
| 60 | IMCX_HOLIDAY_CODE_7 | VARCHAR(6) | No | — | RDB$3825 | NONE/NONE |
| 61 | IMCX_HOLIDAY_CODE_8 | VARCHAR(6) | No | — | RDB$3826 | NONE/NONE |
| 62 | IMCX_HOLIDAY_CODE_9 | VARCHAR(6) | No | — | RDB$3827 | NONE/NONE |
| 63 | IMCX_HOLIDAY_CODE_10 | VARCHAR(6) | No | — | RDB$3828 | NONE/NONE |
| 64 | IMCX_HOLIDAY_CODE_11 | VARCHAR(6) | No | — | RDB$3829 | NONE/NONE |
| 65 | IMCX_HOLIDAY_CODE_12 | VARCHAR(6) | No | — | RDB$3830 | NONE/NONE |
| 66 | IMCX_HOLIDAY_CODE_13 | VARCHAR(6) | No | — | RDB$3831 | NONE/NONE |
| 67 | IMCX_HOLIDAY_CODE_14 | VARCHAR(6) | No | — | RDB$3832 | NONE/NONE |
| 68 | IMCX_HOLIDAY_CODE_15 | VARCHAR(6) | No | — | RDB$3833 | NONE/NONE |
| 69 | IMCX_HOLIDAY_CODE_16 | VARCHAR(6) | No | — | RDB$3834 | NONE/NONE |
| 70 | IMCX_HOLIDAY_CODE_17 | VARCHAR(6) | No | — | RDB$3835 | NONE/NONE |
| 71 | IMCX_HOLIDAY_CODE_18 | VARCHAR(6) | No | — | RDB$3836 | NONE/NONE |
| 72 | IMCX_HOLIDAY_CODE_19 | VARCHAR(6) | No | — | RDB$3837 | NONE/NONE |
| 73 | IMCX_HOLIDAY_CODE_20 | VARCHAR(6) | No | — | RDB$3838 | NONE/NONE |
| 74 | IMCX_HOLIDAY_CODE_21 | VARCHAR(6) | No | — | RDB$3839 | NONE/NONE |
| 75 | IMCX_HOLIDAY_CODE_22 | VARCHAR(6) | No | — | RDB$3840 | NONE/NONE |
| 76 | IMCX_HOLIDAY_CODE_23 | VARCHAR(6) | No | — | RDB$3841 | NONE/NONE |
| 77 | IMCX_HOLIDAY_CODE_24 | VARCHAR(6) | No | — | RDB$3842 | NONE/NONE |
| 78 | IMCX_NOTES | VARCHAR(256) | No | — | RDB$3843 | NONE/NONE |
| 79 | IMCX_SEQ | BIGINT | No | — | RDB$3844 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IMCX_SEQ_KEY | IMCX_SEQ | Yes | Yes | ASC | — |
| IM_CONT_CHRG_MX_PRIME | IMCX_PD_OR_STOR, IMCX_SCAC, IMCX_EQ_TYP, IMCX_DESCID_1, IMCX_DESCID_2, IMCX_DESCID_3, IMCX_SHIP_ID, IMCX_CONS_ID, IMCX_BILLTO_ID, IMCX_WORK_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R145 | IM_CONT_CHRG_MX.(IMCX_BILLTO_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R146 | IM_CONT_CHRG_MX.(IMCX_CONS_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R147 | IM_CONT_CHRG_MX.(IMCX_SHIP_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`IMCX_SEQ_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
