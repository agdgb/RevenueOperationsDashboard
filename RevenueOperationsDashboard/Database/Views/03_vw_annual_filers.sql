-- ====================================================================
-- View: [ops].[vw_annual_filers]
-- Description: Annual Filing Totals View with clean MECE aggregation
--              from core Plans & PlanValues, isolated from cartesian joins.
-- ====================================================================

CREATE OR ALTER VIEW [ops].[vw_annual_filers]
AS
SELECT 
    p.ParentId,
    p.FiscalYearId,
    p.MonthId,
    SUM(CASE WHEN pi.Name LIKE '%profit%' THEN ISNULL(pv.Actual, 0) ELSE 0 END) AS ActualProfit,
    SUM(CASE WHEN pi.Name LIKE '%profit%' THEN ISNULL(pv.Target, 0) ELSE 0 END) AS TargetProfit,
    SUM(CASE WHEN pi.Name LIKE '%rent%' THEN ISNULL(pv.Actual, 0) ELSE 0 END) AS ActualRent,
    SUM(CASE WHEN pi.Name LIKE '%rent%' THEN ISNULL(pv.Target, 0) ELSE 0 END) AS TargetRent,
    SUM(ISNULL(pv.Actual, 0)) AS ActualTotal,
    SUM(ISNULL(pv.Target, 0)) AS TargetTotal
FROM dbo.Plans p
INNER JOIN dbo.PlanItems pi ON p.PlanItemId = pi.Id
INNER JOIN dbo.PlanValues pv ON p.Id = pv.PlanBaseId
WHERE p.IsDeleted = 0
  AND p.PlanType = 'Monthly'
  AND pi.CategoryId = '0CFA5AA5-DB65-4534-83EE-06D471DF7DAC' -- Annual Filer Category
  AND pv.MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40' -- Taxpayer Count Unit
GROUP BY 
    p.ParentId, 
    p.FiscalYearId,
    p.MonthId;
GO
