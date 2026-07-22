IF OBJECT_ID('[dbo].[vw_RevenuePerformance_Clean]') IS NOT NULL 
    DROP VIEW [dbo].[vw_RevenuePerformance_Clean];
GO

CREATE VIEW [dbo].[vw_RevenuePerformance_Clean] AS
SELECT 
    p.ParentId,
    pr.ParentName,
    p.FiscalYearId,
    fy.FiscalYearName,
    p.MonthId,
    m.MonthName,
    m.MonthOrder,
    p.PlanItemId,
    pb.PlanItemName,
    pb.PlanItemNameAm,
    SUM(ISNULL(pv.Target, 0)) AS TotalTarget,
    SUM(ISNULL(pv.Actual, 0)) AS TotalActual
FROM Plans p
INNER JOIN Parents pr ON p.ParentId = pr.ParentId
INNER JOIN FiscalYears fy ON p.FiscalYearId = fy.FiscalYearId
INNER JOIN Months m ON p.MonthId = m.MonthId
INNER JOIN PlanValues pv ON p.Id = pv.PlanBaseId
INNER JOIN [dbo].[vw_PlanItemsBasic] pb ON p.PlanItemId = pb.PlanItemId
WHERE p.PlanType = 'Monthly'
  AND p.IsDeleted = 0
  AND pv.IsDeleted = 0
  AND pv.MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
  AND pb.CategoryId = 'BE6654B7-FB48-434C-AE4D-9D165F44C209'
GROUP BY 
    p.ParentId,
    pr.ParentName,
    p.FiscalYearId,
    fy.FiscalYearName,
    p.MonthId,
    m.MonthName,
    m.MonthOrder,
    p.PlanItemId,
    pb.PlanItemName,
    pb.PlanItemNameAm;
GO
