-- ====================================================================
-- Script: Deploy All [ops] Schema Views
-- Description: Unified migration script that creates the [ops] schema
--              and all 6 isolated operational analytical views.
--              Leaves existing [dbo] views completely intact.
-- ====================================================================

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'ops')
BEGIN
    EXEC('CREATE SCHEMA [ops] AUTHORIZATION [dbo];');
END
GO

-- 1. VAT Declarations Totals View (Dynamic by FiscalYearId & ParentId, divided by active encoded months)
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

-- 2. Payroll Income Tax Totals View (Dynamic by FiscalYearId & ParentId, divided by active encoded months)
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

-- 3. Annual Filing Totals View (Clean MECE aggregation from core Plans & PlanValues, isolated from cartesian joins)
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

-- 4. VAT Filers View (Clean monthly grain from core Plans & PlanValues, zero cartesian join)
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

-- 5. Payroll Income Tax Filers View (Clean monthly grain from core Plans & PlanValues, zero cartesian join)
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

-- 6. Revenue Performance View (Clean monthly grain with native Quarters & pre-calculated selected metrics, zero window function overhead)
CREATE OR ALTER VIEW [ops].[vw_revenue_performance]
AS
SELECT 
    p.ParentId,
    pr.ParentName,
    p.FiscalYearId,
    fy.FiscalYearName,
    fy.IsCurrent AS IsCurrentFiscalYear,
    p.MonthId,
    m.MonthName,
    m.MonthOrder,
    CASE 
        WHEN m.MonthOrder BETWEEN 1 AND 3 THEN 'Q1'
        WHEN m.MonthOrder BETWEEN 4 AND 6 THEN 'Q2'
        WHEN m.MonthOrder BETWEEN 7 AND 9 THEN 'Q3'
        WHEN m.MonthOrder BETWEEN 10 AND 12 THEN 'Q4'
    END AS Quarter,
    CASE 
        WHEN m.MonthOrder BETWEEN 1 AND 3 THEN 1
        WHEN m.MonthOrder BETWEEN 4 AND 6 THEN 2
        WHEN m.MonthOrder BETWEEN 7 AND 9 THEN 3
        WHEN m.MonthOrder BETWEEN 10 AND 12 THEN 4
    END AS QuarterOrder,
    p.PlanItemId,
    pb.PlanItemName,
    SUM(ISNULL(pv.Target, 0)) AS TotalTarget,
    SUM(ISNULL(pv.Actual, 0)) AS TotalActual,
    SUM(CASE 
        WHEN p.PlanItemId IN (
            'EB486830-7C2F-4C28-B2A4-195FDB6641C5',
            '49352433-53B5-466A-88DA-71D146A60C89',
            '98664116-7114-421A-82FD-B05865173EDF'
        ) THEN ISNULL(pv.Target, 0) * 
            CASE 
                WHEN p.FiscalYearId = '2018' THEN CAST(248950784607.89 / 256700000000.00 AS DECIMAL(18, 15))
                WHEN p.FiscalYearId = '2019' THEN CAST(371558025609.84 / 380000000000.00 AS DECIMAL(18, 15))
                ELSE 1.0 
            END
        ELSE 0 
    END) AS SelectedTarget,
    SUM(CASE 
        WHEN p.PlanItemId IN (
            'EB486830-7C2F-4C28-B2A4-195FDB6641C5',
            '49352433-53B5-466A-88DA-71D146A60C89',
            '98664116-7114-421A-82FD-B05865173EDF'
        ) THEN ISNULL(pv.Actual, 0) ELSE 0 
    END) AS SelectedActual
FROM dbo.Plans p
INNER JOIN dbo.Parents pr ON p.ParentId = pr.ParentId
INNER JOIN dbo.FiscalYears fy ON p.FiscalYearId = fy.FiscalYearId
INNER JOIN dbo.Months m ON p.MonthId = m.MonthId
INNER JOIN dbo.PlanValues pv ON p.Id = pv.PlanBaseId
INNER JOIN dbo.vw_PlanItemsBasic pb ON p.PlanItemId = pb.PlanItemId
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
    fy.IsCurrent,
    p.MonthId,
    m.MonthName,
    m.MonthOrder,
    p.PlanItemId,
    pb.PlanItemName;
GO
