-- ====================================================================
-- View: [ops].[vw_incometax_filers]
-- Description: Payroll Income Tax Filers View with clean monthly grain
--              from core Plans & PlanValues, zero cartesian join.
-- ====================================================================

CREATE OR ALTER VIEW [ops].[vw_incometax_filers]
AS
SELECT 
    p.ParentId,
    p.FiscalYearId,
    p.MonthId,
    SUM(ISNULL(pv.Actual, 0)) AS Actual,
    SUM(ISNULL(pv.Target, 0)) AS Target
FROM dbo.Plans p
INNER JOIN dbo.PlanValues pv ON p.Id = pv.PlanBaseId
INNER JOIN dbo.PlanItems pi ON p.PlanItemId = pi.Id
WHERE p.IsDeleted = 0
  AND p.PlanType = 'Monthly'
  AND pi.CategoryId = 'F7D4A0AA-B496-4573-83B6-8AD4B40B39E7' -- IncomeTax Category
  AND pv.MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40' -- Taxpayer Count Unit
GROUP BY 
    p.ParentId,
    p.FiscalYearId,
    p.MonthId;
GO
