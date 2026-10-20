-- ====================================================================
-- View: [ops].[vw_incometax_totals]
-- Description: Payroll Income Tax Totals View with dynamic FiscalYearId &
--              ParentId filtering, and dynamic monthly division based on
--              active encoded months.
-- ====================================================================

CREATE OR ALTER VIEW [ops].[vw_incometax_totals]
AS
SELECT 
    ParentId,
    FiscalYearId,
    SUM(TotalActual) AS Actual,
    SUM(TotalTarget) AS Target,
    ISNULL(ROUND(SUM(TotalActual) / NULLIF(COUNT(DISTINCT CASE WHEN TotalActual > 0 THEN MonthId END), 0), 2), 0) AS ActualAverage,
    ISNULL(ROUND(SUM(TotalTarget) / NULLIF(COUNT(DISTINCT CASE WHEN TotalTarget > 0 THEN MonthId END), 0), 2), 0) AS TargetAverage
FROM [dbo].[View_IncomeTax]
GROUP BY 
    ParentId, 
    FiscalYearId;
GO
