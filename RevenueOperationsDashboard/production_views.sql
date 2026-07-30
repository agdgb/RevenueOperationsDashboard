GO
CREATE OR ALTER VIEW [dbo].[vw_AnnualFiler_Short]
AS

SELECT
    ---------------------------------------------------
    -- ORGANIZATION
    ---------------------------------------------------
    p.ParentId,
    pr.ParentName,

    p.ChildId,
    ch.ChildName,

    ---------------------------------------------------
    -- FISCAL
    ---------------------------------------------------
    p.FiscalYearId,
    fy.FiscalYearName,

    p.MonthId,
    m.MonthName,
    m.MonthId AS MonthOrder,
    ---------------------------------------------------
    -- PLAN ITEM
    ---------------------------------------------------
    p.PlanItemId,
    pi.Name AS PlanItemName,

    ---------------------------------------------------
    -- CATEGORY LEVEL
    ---------------------------------------------------
    CASE
        WHEN pi.Name LIKE '%CATA%' THEN N'ሀ'
        WHEN pi.Name LIKE '%CATB%' THEN N'ለ'
        WHEN pi.Name LIKE '%CATC%' THEN N'ሐ'
        ELSE N'ሌላ'
    END AS CategoryLevel,

    ---------------------------------------------------
    -- EXPECTED
    ---------------------------------------------------
    SUM(pv.Target) AS Expected_Total,

    SUM(CASE
        WHEN pi.Name LIKE '%business profit%'
        THEN pv.Target
        ELSE 0
    END) AS Expected_Profit,

    SUM(CASE
        WHEN pi.Name LIKE '%rent%'
        THEN pv.Target
        ELSE 0
    END) AS Expected_Rent,

    ---------------------------------------------------
    -- DECLARED
    ---------------------------------------------------
    SUM(CASE
        WHEN pi.Name LIKE '%business profit%'
        THEN pv.Actual
        ELSE 0
    END) AS Declared_Profit,

    SUM(CASE
        WHEN pi.Name LIKE '%rent%'
        THEN pv.Actual
        ELSE 0
    END) AS Declared_Rent,

    SUM(pv.Actual) AS Declared_Total,

    ---------------------------------------------------
    -- REMAINING
    ---------------------------------------------------
    SUM(pv.Target) - SUM(pv.Actual)
    AS RemainingAmount,

    ---------------------------------------------------
    -- PERFORMANCE %
    ---------------------------------------------------
    CAST(
        CASE
            WHEN SUM(pv.Target)=0 THEN 0
            ELSE
                SUM(pv.Actual) * 100.0
                / SUM(pv.Target)
        END
    AS DECIMAL(18,2))
    AS PerformancePercent,

    ---------------------------------------------------
    -- COLLECTED
    ---------------------------------------------------
    SUM(
        TRY_CAST(
            JSON_VALUE(
                dpv.SchemaData,
                '$.AmountCollected'
            )
        AS DECIMAL(18,2))
    ) AS CollectedAmount,

    ---------------------------------------------------
    -- LAST YEAR PROFIT
    ---------------------------------------------------
    SUM(CASE
        WHEN prevpi.Name LIKE '%business profit%'
        THEN ISNULL(prevpv.Actual,0)
        ELSE 0
    END) AS LastYearProfit,

    ---------------------------------------------------
    -- LAST YEAR RENT
    ---------------------------------------------------
    SUM(CASE
        WHEN prevpi.Name LIKE '%rent%'
        THEN ISNULL(prevpv.Actual,0)
        ELSE 0
    END) AS LastYearRent,

    ---------------------------------------------------
    -- LAST YEAR TOTAL
    ---------------------------------------------------
    SUM(ISNULL(prevpv.Actual,0))
    AS LastYearTotal,

    ---------------------------------------------------
    -- GROWTH NUMBER
    ---------------------------------------------------
    SUM(pv.Actual)
    -
    SUM(ISNULL(prevpv.Actual,0))
    AS GrowthNumber,

    ---------------------------------------------------
    -- GROWTH PERCENT
    ---------------------------------------------------
    CAST(
        CASE
            WHEN SUM(ISNULL(prevpv.Actual,0))=0
            THEN 0
            ELSE
            (
                SUM(pv.Actual)
                -
                SUM(ISNULL(prevpv.Actual,0))
            ) * 100.0
            /
            SUM(ISNULL(prevpv.Actual,0))
        END
    AS DECIMAL(18,2))
    AS GrowthPercent,

    ---------------------------------------------------
    -- DAILY INFO
    ---------------------------------------------------
    CAST(dpv.EntryDate AS DATE) AS EntryDate,
    MIN(dpv.EntryDate) AS FirstEntryDate,
    MAX(dpv.EntryDate) AS LastEntryDate,
    COUNT(dpv.Id) AS DailyEntryCount,
    SUM(ISNULL(dpv.Value,0)) AS TotalDailyValue

FROM dbo.Plans p

INNER JOIN dbo.PlanItems pi
    ON p.PlanItemId = pi.Id

INNER JOIN dbo.PlanValues pv
    ON p.Id = pv.PlanBaseId

LEFT JOIN dbo.DailyPlanValues dpv
    ON pv.Id = dpv.PlanValueId

INNER JOIN dbo.Parents pr
    ON p.ParentId = pr.ParentId

INNER JOIN dbo.Children ch
    ON p.ChildId = ch.ChildId

INNER JOIN dbo.FiscalYears fy
    ON p.FiscalYearId = fy.FiscalYearId

INNER JOIN dbo.Months m
    ON p.MonthId = m.MonthId

---------------------------------------------------
-- PREVIOUS YEAR
---------------------------------------------------
LEFT JOIN dbo.Plans prevp
    ON p.ParentId = prevp.ParentId
    AND p.ChildId = prevp.ChildId
    AND p.MonthId = prevp.MonthId
    AND p.PlanItemId = prevp.PlanItemId
    AND p.FiscalYearId = prevp.FiscalYearId + 1

LEFT JOIN dbo.PlanItems prevpi
    ON prevp.PlanItemId = prevpi.Id

LEFT JOIN dbo.PlanValues prevpv
    ON prevp.Id = prevpv.PlanBaseId

WHERE p.IsDeleted = 0
AND pi.CategoryId='0CFA5AA5-DB65-4534-83EE-06D471DF7DAC'

GROUP BY

    p.ParentId,
    pr.ParentName,

    p.ChildId,
    ch.ChildName,

    p.FiscalYearId,
    fy.FiscalYearName,

    p.MonthId,
    m.MonthName,
    m.MonthId,

    p.PlanItemId,
    pi.Name,
    CAST(dpv.EntryDate AS DATE),
    CASE
        WHEN pi.Name LIKE '%CATA%' THEN N'ሀ'
        WHEN pi.Name LIKE '%CATB%' THEN N'ለ'
        WHEN pi.Name LIKE '%CATC%' THEN N'ሐ'
        ELSE N'ሌላ'
    END
GO
CREATE OR ALTER VIEW [dbo].[vw_DebtCollection]
AS

WITH BaseData AS (
    SELECT
       p.ParentId, 
       pr.ParentName, 
        p.ChildId,
        ch.ChildName,

        p.FiscalYearId,
        fy.FiscalYearName,
        fy.IsCurrent AS IsCurrentFiscalYear,

        m.MonthId,
        m.MonthName,
        m.MonthOrder,

        p.MonthlyStatus,
        CASE 
            WHEN p.MonthlyStatus = 'Waiting' THEN 'Waiting' 
            WHEN p.MonthlyStatus = 'Ongoing' THEN 'Active' 
            WHEN p.MonthlyStatus = 'Completed' THEN 'Completed' 
            WHEN p.MonthlyStatus = 'Closed' THEN 'Closed' 
            ELSE 'Unknown'
        END AS StatusDisplay,

        pi.Id AS PlanItemId,
        pv.MeasurementUnitId,

        pv.Target,
        pv.Actual,
        pv.BroughtForward,
        (pv.Target + pv.BroughtForward) AS TotalTarget,

        CASE 
            WHEN (pv.Target + pv.BroughtForward) > 0 
            THEN (pv.Actual / (pv.Target + pv.BroughtForward)) * 100 
            ELSE 0 
        END AS AchievementPercentage,

        jf.[key] AS JsonKey,
        TRY_CONVERT(DECIMAL(18,2), jf.[value]) AS JsonValue

    FROM dbo.Plans p
    INNER JOIN dbo.Parents pr ON p.ParentId = pr.ParentId 
    INNER JOIN dbo.Children ch ON p.ChildId = ch.ChildId
    INNER JOIN dbo.FiscalYears fy ON p.FiscalYearId = fy.FiscalYearId
    INNER JOIN dbo.Months m ON p.MonthId = m.MonthId
    INNER JOIN dbo.PlanItems pi ON p.PlanItemId = pi.Id
    INNER JOIN dbo.PlanCategories pc ON pi.CategoryId = pc.Id
    INNER JOIN dbo.PlanValues pv ON p.Id = pv.PlanBaseId

    LEFT JOIN dbo.DailyPlanValues dpv
        ON pv.Id = dpv.PlanValueId
        AND dpv.IsDeleted = 0

    OUTER APPLY OPENJSON(dpv.SchemaData) jf

    WHERE p.PlanType = 'Monthly'
      AND p.IsDeleted = 0
      AND pc.Id = '78C1B58A-922F-4A45-B4EC-BE649C77C1AF' -- Debt Collection
)

SELECT
    ParentId,  
    ParentName, 
    ChildId,
    ChildName,
    FiscalYearId,
    FiscalYearName,
    IsCurrentFiscalYear,
    MonthId,
    MonthName,
    MonthOrder,
    MonthlyStatus,
    StatusDisplay,

    -------------------------------------------------
    -- TAXPAYER KPI
    -------------------------------------------------
    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN Target END) AS DebtcollectionperformanceTaxpayer_Target,

    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN Actual END) AS DebtcollectionperformanceTaxpayer_Actual,

    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN BroughtForward END) AS DebtcollectionperformanceTaxpayer_BroughtForward,

    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN TotalTarget END) AS DebtcollectionperformanceTaxpayer_TotalTarget,

    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN AchievementPercentage END) AS DebtcollectionperformanceTaxpayer_AchievementPercentage,

    -------------------------------------------------
    -- CURRENCY KPI
    -------------------------------------------------
    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
        THEN Target END) AS DebtcollectionperformanceCurrency_Target,

    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
        THEN Actual END) AS DebtcollectionperformanceCurrency_Actual,

    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
        THEN BroughtForward END) AS DebtcollectionperformanceCurrency_BroughtForward,

    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
        THEN TotalTarget END) AS DebtcollectionperformanceCurrency_TotalTarget,

    MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
        THEN AchievementPercentage END) AS DebtcollectionperformanceCurrency_AchievementPercentage,
-- UPDATED TOTAL TAXPAYER
(
    ISNULL(MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN Target END),0)

  + ISNULL(MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN BroughtForward END),0)

  + ISNULL(SUM(CASE 
        WHEN JsonKey = 'Numberoftaxpayersaddedpermonth' 
        THEN JsonValue END),0)
) AS totaltaxpayer,
-- UPDATED TOTAL DEBIT CURRENCY
(
    ISNULL(MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
        THEN Target END),0)

  + ISNULL(MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
        THEN BroughtForward END),0)

  + ISNULL(SUM(CASE 
        WHEN JsonKey = 'monthlyaddedamount' 
        THEN JsonValue END),0)
) AS totaldebitcurrency,

-- TAXPAYER PERFORMANCE PERCENTAGE
(
    ISNULL(MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN Actual END),0)

    /
    NULLIF(
        (
            ISNULL(MAX(CASE 
                WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
                 AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
                THEN Target END),0)

          + ISNULL(MAX(CASE 
                WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
                 AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
                THEN BroughtForward END),0)

          + ISNULL(SUM(CASE 
                WHEN JsonKey = 'Numberoftaxpayersaddedpermonth'
                THEN JsonValue END),0)
        ), 0
    ) * 100
) AS percentage,
-- REMAINING BY TAXPAYER
(
    (
        ISNULL(MAX(CASE 
            WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
             AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
            THEN Target END),0)

      + ISNULL(MAX(CASE 
            WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
             AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
            THEN BroughtForward END),0)

      + ISNULL(SUM(CASE 
            WHEN JsonKey = 'Numberoftaxpayersaddedpermonth'
            THEN JsonValue END),0)
    )
    -
    ISNULL(MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
        THEN Actual END),0)
) AS remaininbytaxpayer,
-- REMAINING BY CURRENCY
(
    (
        ISNULL(MAX(CASE 
            WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
             AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
            THEN Target END),0)
      + ISNULL(MAX(CASE 
            WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
             AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
            THEN BroughtForward END),0)
      + ISNULL(SUM(CASE 
            WHEN JsonKey = 'monthlyaddedamount'
            THEN JsonValue END),0)
    )
    -
    ISNULL(MAX(CASE 
        WHEN PlanItemId = 'D0386E52-258C-4F9F-BD5F-E7AD24EFE565'
         AND MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
        THEN Actual END),0)
) AS remainigbycurrency,
-- TOTAL COLLECTED AMOUNT
(
    ISNULL(SUM(CASE WHEN JsonKey = 'blockedistimatedcollectedamount' THEN JsonValue END),0)
  + ISNULL(SUM(CASE WHEN JsonKey = 'collectedamountfrombid' THEN JsonValue END),0)
) AS totalcollectedamount,

    -------------------------------------------------
    -- JSON DATA (OTHER)
    -------------------------------------------------
    SUM(CASE WHEN JsonKey = 'Numberoftaxpayersaddedpermonth' THEN JsonValue END) AS Numberoftaxpayersaddedpermonth,
    SUM(CASE WHEN JsonKey = 'monthlyaddedamount' THEN JsonValue END) AS monthlyaddedamount,
    SUM(CASE WHEN JsonKey = 'blockbybanktransfersbytaxpayers' THEN JsonValue END) AS blockbybanktransfersbytaxpayers,
    SUM(CASE WHEN JsonKey = 'responsegivenblock' THEN JsonValue END) AS responsegivenblock,
    SUM(CASE WHEN JsonKey = 'blockedistimatedcollectedamount' THEN JsonValue END) AS blockedistimatedcollectedamount,
    SUM(CASE WHEN JsonKey = 'Vehicleblockednumber' THEN JsonValue END) AS Vehicleblockednumber,
    SUM(CASE WHEN JsonKey = 'Vehicleholdonnumber' THEN JsonValue END) AS Vehicleholdonnumber,
    SUM(CASE WHEN JsonKey = 'Vehicletransferedtobureaunumber' THEN JsonValue END) AS Vehicletransferedtobureaunumber,
    SUM(CASE WHEN JsonKey = 'blockedhousenumber' THEN JsonValue END) AS blockedhousenumber,
    SUM(CASE WHEN JsonKey = 'holdonhousenumber' THEN JsonValue END) AS holdonhousenumber,
    SUM(CASE WHEN JsonKey = 'transferedtobureauhousenumber' THEN JsonValue END) AS transferedtobureauhousenumber,
    SUM(CASE WHEN JsonKey = 'otherstreasureblockednumber' THEN JsonValue END) AS otherstreasureblockednumber,
    SUM(CASE WHEN JsonKey = 'otherstreasureholdonnumber' THEN JsonValue END) AS otherstreasureholdonnumber,
    SUM(CASE WHEN JsonKey = 'otherstreasuretransferedtobureaunumber' THEN JsonValue END) AS otherstreasuretransferedtobureaunumber,
    SUM(CASE WHEN JsonKey = 'bidtreasurenumber' THEN JsonValue END) AS bidtreasurenumber,
    SUM(CASE WHEN JsonKey = 'bidtreasurecollectedamount' THEN JsonValue END) AS bidtreasurecollectedamount,
    SUM(CASE WHEN JsonKey = 'collectedamountfrombid' THEN JsonValue END) AS collectedamountfrombid


FROM BaseData
GROUP BY
    ParentId,  
    ParentName, 
    ChildId,
    ChildName,
    FiscalYearId,
    FiscalYearName,
    IsCurrentFiscalYear,
    MonthId,
    MonthName,
    MonthOrder,
    MonthlyStatus,
    StatusDisplay;
GO
CREATE OR ALTER VIEW  vw_incometax_totals 
as
SELECT 
    ParentId,
    FiscalYearId,
    SUM(TotalActual) AS Actual,
    SUM(TotalTarget) AS Target,
    AVG(CAST(TotalActual AS DECIMAL(18,2))) AS ActualAverage,
    AVG(CAST(TotalTarget AS DECIMAL(18,2))) AS TargetAverage
FROM View_IncomeTax where fiscalyearid = 2018 
GROUP BY 
    ParentId, 
    FiscalYearId
GO
CREATE OR ALTER VIEW [dbo].[vw_RenwalClearance]
AS
SELECT 
    -- Organization hierarchy
    p.ParentId,
    pr.ParentName,
    p.ChildId,
    ch.ChildName,

    -- Fiscal year
    p.FiscalYearId,
    fy.FiscalYearName,
    fy.IsCurrent AS IsCurrentFiscalYear,

    -- Month
    m.MonthId,
    m.MonthName,
    m.MonthOrder,

    -- Plan item
    p.PlanItemId,
    pi.Name AS PlanItemName,

    pc.Id AS CategoryId,
    pc.Name AS CategoryName,
    pc.NameAm AS CategoryNameAm,

    pi.ActualsSchema AS PlanItemActualsSchema,

    -- Monthly metrics
    pv.MeasurementUnitId,
    mu.Name AS MeasurementUnitName,
    pv.Target,
    pv.Actual,
    pv.BroughtForward,
    (pv.Target + pv.BroughtForward) AS TotalTarget,
    pv.ActualTarget AS PlanValueActualTarget,

    -- Daily entry details
    dpv.Id AS DailyPlanValueId,
    dpv.EntryDate,
    dpv.Status AS DailyStatus,
    dpv.SubmittedAt,
    dpv.ApprovedAt,
    dpv.RejectedAt,
    dpv.LockedAt,
    dpv.RejectionReason,
    dpv.RejectedByUserId,
    dpv.CreatedByUserId AS DailyCreatedByUserId,
    dpv.CreatedDate AS DailyCreatedDate,
    dpv.UpdatedByUserId AS DailyUpdatedByUserId,
    dpv.UpdatedDate AS DailyUpdatedDate,

    -- JSON fields from SchemaData
    daily_fields.[key]   AS DailyFieldName,
    daily_fields.[value] AS DailyFieldValue,

    -- Achievement %
    CASE 
        WHEN (pv.Target + pv.BroughtForward) > 0 
        THEN (pv.Actual * 100.0) / (pv.Target + pv.BroughtForward)
        ELSE 0
    END AS AchievementPercentage,

    -- Monthly status
    p.MonthlyStatus,

    CASE 
        --WHEN p.MonthlyStatus = 1 THEN 'Waiting'
        --WHEN p.MonthlyStatus = 2 THEN 'Active'
        --WHEN p.MonthlyStatus = 3 THEN 'Completed'
        --WHEN p.MonthlyStatus = 4 THEN 'Closed'
            WHEN p.MonthlyStatus = 'Waiting' THEN 'Waiting' 
            WHEN p.MonthlyStatus = 'Ongoing' THEN 'Active' 
            WHEN p.MonthlyStatus = 'Completed' THEN 'Completed' 
            WHEN p.MonthlyStatus = 'Closed' THEN 'Closed'
        ELSE 'Unknown'
    END AS StatusDisplay,

    -- Fiscal year rank
    DENSE_RANK() OVER (ORDER BY fy.FiscalYearId) AS FiscalYearRank

FROM dbo.Plans p
INNER JOIN dbo.Parents pr 
    ON p.ParentId = pr.ParentId

INNER JOIN dbo.Children ch 
    ON p.ChildId = ch.ChildId

INNER JOIN dbo.FiscalYears fy 
    ON p.FiscalYearId = fy.FiscalYearId

INNER JOIN dbo.Months m 
    ON p.MonthId = m.MonthId

INNER JOIN dbo.PlanItems pi 
    ON p.PlanItemId = pi.Id

INNER JOIN dbo.PlanCategories pc 
    ON pi.CategoryId = pc.Id

INNER JOIN dbo.PlanValues pv 
    ON p.Id = pv.PlanBaseId

INNER JOIN dbo.PlanMeasurementUnits mu 
    ON pv.MeasurementUnitId = mu.Id

-- keep monthly rows even if no daily data
LEFT JOIN dbo.DailyPlanValues dpv 
    ON pv.Id = dpv.PlanValueId
    AND dpv.IsDeleted = 0

-- parse JSON schema
OUTER APPLY OPENJSON(dpv.SchemaData) AS daily_fields

WHERE 
    p.PlanType = 'Monthly'
    AND p.IsDeleted = 0
    AND p.PlanItemId IN (
        'DAB8FA40-AA17-4433-840C-9D0B572E6BAB',
        'CB600116-7DF1-48D5-9D03-8961FAE9CF7B',
        '3ED4EB00-7324-46D9-AA4D-4C6A0E9BEA9F'
    )
    AND pv.MeasurementUnitId IN (
        '7018221B-D2B4-4C98-A1A3-B8C04BF44C40'
    );
GO
CREATE OR ALTER VIEW [dbo].[vw_RevenuePerformance]
AS
WITH BaseData AS (
    SELECT 
        p.ParentId, pr.ParentName,
        p.FiscalYearId, fy.FiscalYearName, fy.IsCurrent AS IsCurrentFiscalYear,
        m.MonthId, m.MonthName, m.MonthOrder,
        p.PlanItemId,
        pv.Target,
        pv.Actual,
        pv.BroughtForward,
        (ISNULL(pv.Target, 0) + ISNULL(pv.BroughtForward, 0)) AS TotalTarget,
        DENSE_RANK() OVER (ORDER BY fy.FiscalYearId) AS FiscalYearRank
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
),
AggregatedData AS (
    SELECT 
        ParentId, ParentName,
        FiscalYearId, FiscalYearName, IsCurrentFiscalYear,
        MonthId, MonthName, MonthOrder,
        FiscalYearRank,
        SUM(Target) AS TotalTarget,
        SUM(Actual) AS TotalActual,
        SUM(BroughtForward) AS TotalBroughtForward,
        SUM(CASE 
            WHEN PlanItemId IN (
                'EB486830-7C2F-4C28-B2A4-195FDB6641C5',
                '49352433-53B5-466A-88DA-71D146A60C89',
                '98664116-7114-421A-82FD-B05865173EDF'
            ) THEN Actual ELSE 0 
        END) AS TotalSelectedActual,
        SUM(CASE 
            WHEN PlanItemId IN (
                'EB486830-7C2F-4C28-B2A4-195FDB6641C5',
                '49352433-53B5-466A-88DA-71D146A60C89',
                '98664116-7114-421A-82FD-B05865173EDF'
            ) THEN Target ELSE 0 
        END) AS TotalSelectedTarget,
        SUM(CASE 
            WHEN PlanItemId IN (
                'EB486830-7C2F-4C28-B2A4-195FDB6641C5',
                '49352433-53B5-466A-88DA-71D146A60C89',
                '98664116-7114-421A-82FD-B05865173EDF'
            ) THEN Actual ELSE 0 
        END) AS CurrentSelectedActual,
        SUM(CASE 
            WHEN PlanItemId IN (
                'EB486830-7C2F-4C28-B2A4-195FDB6641C5',
                '49352433-53B5-466A-88DA-71D146A60C89',
                '98664116-7114-421A-82FD-B05865173EDF'
            ) THEN Target ELSE 0 
        END) AS CurrentSelectedTarget,
        COUNT(DISTINCT PlanItemId) AS MetricCount,
        CASE 
            WHEN SUM(Target) > 0 THEN (SUM(Actual) / SUM(Target)) * 100 ELSE 0 
        END AS OverallAchievement,
        CASE 
            WHEN SUM(Actual) >= SUM(Target) THEN 'Exceeded'
            WHEN SUM(Actual) >= SUM(Target) * 0.9 THEN 'On Track'
            WHEN SUM(Actual) >= SUM(Target) * 0.75 THEN 'Behind'
            ELSE 'Critical'
        END AS PerformanceStatus
    FROM BaseData
    GROUP BY 
        ParentId, ParentName,
        FiscalYearId, FiscalYearName, IsCurrentFiscalYear,
        MonthId, MonthName, MonthOrder,
        FiscalYearRank
)
SELECT 
    ParentId, ParentName,
    FiscalYearId, FiscalYearName, IsCurrentFiscalYear,
    MonthId, MonthName, MonthOrder,
    TotalTarget,
    TotalActual,
    TotalBroughtForward,
    TotalSelectedActual,
    TotalSelectedTarget,
    MetricCount,
    OverallAchievement,
    PerformanceStatus,
    SUM(TotalActual) OVER (
        PARTITION BY ParentId, FiscalYearId 
        ORDER BY MonthOrder
        ROWS UNBOUNDED PRECEDING
    ) AS YTDActual,
    SUM(TotalTarget) OVER (
        PARTITION BY ParentId, FiscalYearId 
        ORDER BY MonthOrder
        ROWS UNBOUNDED PRECEDING
    ) AS YTDTarget,
    LAG(TotalActual, 12) OVER (
        PARTITION BY ParentId 
        ORDER BY FiscalYearRank, MonthOrder
    ) AS PreviousYearSameMonth,
    LAG(TotalTarget, 12) OVER (
        PARTITION BY ParentId 
        ORDER BY FiscalYearRank, MonthOrder
    ) AS PreviousYearTarget,
    LAG(CurrentSelectedActual, 12) OVER (
        PARTITION BY ParentId 
        ORDER BY FiscalYearRank, MonthOrder
    ) AS PreviousYearSameMonthSelected,
    LAG(CurrentSelectedTarget, 12) OVER (
        PARTITION BY ParentId 
        ORDER BY FiscalYearRank, MonthOrder
    ) AS PreviousYearSelectedTarget,
    TotalActual - LAG(TotalActual, 12) OVER (
        PARTITION BY ParentId 
        ORDER BY FiscalYearRank, MonthOrder
    ) AS YoYChange,
    CASE 
        WHEN LAG(TotalActual, 12) OVER (
            PARTITION BY ParentId 
            ORDER BY FiscalYearRank, MonthOrder
        ) > 0 
        THEN (TotalActual - LAG(TotalActual, 12) OVER (
            PARTITION BY ParentId 
            ORDER BY FiscalYearRank, MonthOrder
        )) / LAG(TotalActual, 12) OVER (
            PARTITION BY ParentId 
            ORDER BY FiscalYearRank, MonthOrder
        )
        ELSE NULL 
    END AS YoYGrowthPercentage,
    TotalTarget - LAG(TotalTarget, 12) OVER (
        PARTITION BY ParentId 
        ORDER BY FiscalYearRank, MonthOrder
    ) AS TargetVariance
FROM AggregatedData;
GO
CREATE OR ALTER VIEW [dbo].[vw_TaxAudit]
AS
WITH BaseData AS
(
    SELECT
        p.ParentId,
        pr.ParentName AS BranchName,
        p.ChildId,
        ch.ChildName,
        p.FiscalYearId,
        fy.FiscalYearName,
        fy.IsCurrent AS IsCurrentFiscalYear,
        m.MonthId,
        m.MonthName,
        m.MonthOrder,
        p.PlanItemId,
        pi.Name AS PlanItemName,
        pc.Id AS CategoryId,
        pc.Name AS CategoryName,
        pc.NameAm AS CategoryNameAm,
        pv.MeasurementUnitId,
        mu.Name AS MeasurementUnitName,
        pv.Target,
        pv.Actual,
        pv.BroughtForward,
        (pv.Target + pv.BroughtForward) AS TotalTarget,
        CASE
            WHEN (pv.Target + pv.BroughtForward) > 0
                THEN (pv.Actual / (pv.Target + pv.BroughtForward)) * 100
            ELSE 0
        END AS AchievementPercentage,
        p.MonthlyStatus,
        CASE p.MonthlyStatus
            --WHEN 1 THEN 'Waiting'
            --WHEN 2 THEN 'Active'
            --WHEN 3 THEN 'Completed'
            --WHEN 4 THEN 'Closed'
            WHEN 'Waiting' THEN 'Waiting' 
            WHEN 'Ongoing' THEN 'Active' 
            WHEN 'Completed' THEN 'Completed' 
            WHEN 'Closed' THEN 'Closed'
            ELSE 'Unknown'
        END AS StatusDisplay,
        DENSE_RANK() OVER (ORDER BY fy.FiscalYearId) AS FiscalYearRank
    FROM Plans p
    INNER JOIN Parents pr ON p.ParentId = pr.ParentId
    INNER JOIN Children ch ON p.ChildId = ch.ChildId
    INNER JOIN FiscalYears fy ON p.FiscalYearId = fy.FiscalYearId
    INNER JOIN Months m ON p.MonthId = m.MonthId
    INNER JOIN PlanItems pi ON p.PlanItemId = pi.Id
    INNER JOIN PlanCategories pc ON pi.CategoryId = pc.Id
    INNER JOIN PlanValues pv ON p.Id = pv.PlanBaseId
    INNER JOIN PlanMeasurementUnits mu ON pv.MeasurementUnitId = mu.Id
    WHERE
        p.PlanType = 'Monthly'
        AND p.IsDeleted = 0
        AND p.PlanItemId IN (
            'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9',   -- Limited Audit
            '8866431C-2875-4F3C-800C-3D87365F3161',   -- Regular Audit
            '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F'    -- Closing Audit
        )
        -- Remove next line if you need all fiscal years
        -- AND fy.IsCurrent = 1
)
SELECT
    BranchName,
    FiscalYearName,
    MonthId,
    MonthName,
    MonthOrder,
    PlanItemId,                
    PlanItemName,              
    CategoryName, 

    -- ==================== FILE SECTION (GUID: CC621204-433D-415F-B58D-11550483C045) ====================
    -- Regular Audit
    SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Target ELSE 0 END) AS File_Reg_Target,
    SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Actual ELSE 0 END) AS File_Reg_Actual,
    CASE
        WHEN SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Target ELSE 0 END) > 0
        THEN SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Actual ELSE 0 END) * 100.0 /
             SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Target ELSE 0 END)
        ELSE 0
    END AS File_Reg_Percent,

    -- Closing Audit
    SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Target ELSE 0 END) AS File_Cls_Target,
    SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Actual ELSE 0 END) AS File_Cls_Actual,
    CASE
        WHEN SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Target ELSE 0 END) > 0
        THEN SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Actual ELSE 0 END) * 100.0 /
             SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Target ELSE 0 END)
        ELSE 0
    END AS File_Cls_Percent,

    -- Limited Audit
    SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Target ELSE 0 END) AS File_Ltd_Target,
    SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Actual ELSE 0 END) AS File_Ltd_Actual,
    CASE
        WHEN SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Target ELSE 0 END) > 0
        THEN SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Actual ELSE 0 END) * 100.0 /
             SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Target ELSE 0 END)
        ELSE 0
    END AS File_Ltd_Percent,

    -- Total File
    SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' THEN Target ELSE 0 END) AS File_Total_Target,
    SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' THEN Actual ELSE 0 END) AS File_Total_Actual,
    CASE
        WHEN SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' THEN Target ELSE 0 END) > 0
        THEN SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' THEN Actual ELSE 0 END) * 100.0 /
             SUM(CASE WHEN MeasurementUnitId = 'CC621204-433D-415F-B58D-11550483C045' THEN Target ELSE 0 END)
        ELSE 0
    END AS File_Total_Percent,

    -- ==================== MONEY SECTION (GUID: 541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0) ====================
    -- Regular Audit
    SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Target ELSE 0 END) AS Money_Reg_Target,
    SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Actual ELSE 0 END) AS Money_Reg_Actual,
    CASE
        WHEN SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Target ELSE 0 END) > 0
        THEN SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Actual ELSE 0 END) * 100.0 /
             SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '8866431C-2875-4F3C-800C-3D87365F3161' THEN Target ELSE 0 END)
        ELSE 0
    END AS Money_Reg_Percent,

    -- Closing Audit
    SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Target ELSE 0 END) AS Money_Cls_Target,
    SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Actual ELSE 0 END) AS Money_Cls_Actual,
    CASE
        WHEN SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Target ELSE 0 END) > 0
        THEN SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Actual ELSE 0 END) * 100.0 /
             SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = '6526E33E-DA0D-45C1-A8EB-F682C9F3C13F' THEN Target ELSE 0 END)
        ELSE 0
    END AS Money_Cls_Percent,

    -- Limited Audit
    SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Target ELSE 0 END) AS Money_Ltd_Target,
    SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Actual ELSE 0 END) AS Money_Ltd_Actual,
    CASE
        WHEN SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Target ELSE 0 END) > 0
        THEN SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Actual ELSE 0 END) * 100.0 /
             SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' AND PlanItemId = 'E8F0AB38-5D2F-4760-87D8-1040D94A0FA9' THEN Target ELSE 0 END)
        ELSE 0
    END AS Money_Ltd_Percent,

    -- Total Money
    SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' THEN Target ELSE 0 END) AS Money_Total_Target,
    SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' THEN Actual ELSE 0 END) AS Money_Total_Actual,
    CASE
        WHEN SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' THEN Target ELSE 0 END) > 0
        THEN SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' THEN Actual ELSE 0 END) * 100.0 /
             SUM(CASE WHEN MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0' THEN Target ELSE 0 END)
        ELSE 0
    END AS Money_Total_Percent

FROM BaseData
GROUP BY
    ParentId,
    BranchName,
    FiscalYearId,
    FiscalYearName,
    MonthId,
    MonthName,
    MonthOrder,
    PlanItemId,                
    PlanItemName,              
    CategoryName; 
GO
CREATE OR ALTER VIEW  [dbo].[vw_vat_totals] 
as
SELECT 
    ParentId,
    FiscalYearId,
    SUM(Taxpayer_Actual) AS Actual,
    SUM(VATTaxpayer_Target) AS Target,
    AVG(CAST(Taxpayer_Actual AS DECIMAL(18,2))) AS ActualAverage,
    AVG(CAST(VATTaxpayer_Target AS DECIMAL(18,2))) AS TargetAverage
FROM vw_MonthlyVATFiler where fiscalyearid = 2018 
GROUP BY 
    ParentId, 
    FiscalYearId


