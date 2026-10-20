-- ====================================================================
-- View: [ops].[vw_vat_totals]
-- Description: VAT Declarations Totals View with dynamic FiscalYearId &
--              ParentId filtering, and dynamic monthly division based on
--              active encoded months.
-- ====================================================================

CREATE OR ALTER VIEW [ops].[vw_vat_totals]
AS
SELECT 
    ParentId,
    FiscalYearId,
    SUM(Taxpayer_Actual) AS Actual,
    SUM(VATTaxpayer_Target) AS Target,
    ISNULL(ROUND(SUM(Taxpayer_Actual) / NULLIF(COUNT(DISTINCT CASE WHEN Taxpayer_Actual > 0 THEN MonthId END), 0), 2), 0) AS ActualAverage,
    ISNULL(ROUND(SUM(VATTaxpayer_Target) / NULLIF(COUNT(DISTINCT CASE WHEN VATTaxpayer_Target > 0 THEN MonthId END), 0), 2), 0) AS TargetAverage
FROM [dbo].[vw_MonthlyVATFiler]
GROUP BY 
    ParentId, 
    FiscalYearId;
GO
