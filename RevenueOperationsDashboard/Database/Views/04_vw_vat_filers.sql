-- ====================================================================
-- View: [ops].[vw_vat_filers]
-- Description: VAT Filers View with clean monthly grain from core Plans
--              and PlanValues, zero cartesian join.
-- ====================================================================

CREATE OR ALTER VIEW [ops].[vw_vat_filers]
AS
SELECT 
    p.ParentId,
    p.FiscalYearId,
    p.MonthId,
    SUM(ISNULL(pv.Actual, 0)) AS Actual,
    SUM(ISNULL(pv.Target, 0)) AS Target
FROM dbo.Plans p
INNER JOIN dbo.PlanValues pv ON p.Id = pv.PlanBaseId
WHERE p.IsDeleted = 0
  AND p.PlanType = 'Monthly'
  AND p.PlanItemId = '58F152E2-6FF2-40DD-8159-15AAB7A44080' -- VAT Expected
  AND pv.MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40' -- Taxpayer Count Unit
GROUP BY 
    p.ParentId,
    p.FiscalYearId,
    p.MonthId;
GO
