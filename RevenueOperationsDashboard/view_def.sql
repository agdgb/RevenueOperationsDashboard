Text                                                                                                                                                                                                                                                           
---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                                                                                                                                                                                                                                                             
CREATE   VIEW [dbo].[vw_RevenuePerformance_Detail]
                                                                                                                                                                                                           
AS
                                                                                                                                                                                                                                                           
WITH BaseData AS (
                                                                                                                                                                                                                                           
    SELECT 
                                                                                                                                                                                                                                                  
        p.ParentId, pr.ParentName, p.ChildId, ch.ChildName,
                                                                                                                                                                                                  
        p.FiscalYearId, fy.FiscalYearName, fy.IsCurrent AS IsCurrentFiscalYear,
                                                                                                                                                                              
        m.MonthId, m.MonthName, m.MonthOrder,
                                                                                                                                                                                                                
        p.PlanItemId,
                                                                                                                                                                                                                                        
        pv.Target,
                                                                                                                                                                                                                                           
        pv.Actual,
                                                                                                                                                                                                                                           
        pv.BroughtForward,
                                                                                                                                                                                                                                   
        (ISNULL(pv.Target, 0) + ISNULL(pv.BroughtForward, 0)) AS TotalTarget,
                                                                                                                                                                                
        CASE 
                                                                                                                                                                                                                                                
            WHEN p.PlanItemId IN (
                                                                                                                                                                                                                           
                'EB486830-7C2F-4C28-B2A4-195FDB6641C5',
                                                                                                                                                                                                      
                '49352433-53B5-466A-88DA-71D146A60C89',
                                                                                                                                                                                                      
                '98664116-7114-421A-82FD-B05865173EDF'
                                                                                                                                                                                                       
            ) THEN 1 ELSE 0 
                                                                                                                                                                                                                                 
        END AS IsSelectedPlanItem,
                                                                                                                                                                                                                           
        DENSE_RANK() OVER (ORDER BY fy.FiscalYearId) AS FiscalYearRank
                                                                                                                                                                                       
    FROM Plans p
                                                                                                                                                                                                                                             
    INNER JOIN Parents pr ON p.ParentId = pr.ParentId
                                                                                                                                                                                                        
    INNER JOIN Children ch ON p.ChildId = ch.ChildId
                                                                                                                                                                                                         
    INNER JOIN FiscalYears fy ON p.FiscalYearId = fy.FiscalYearId
                                                                                                                                                                                            
    INNER JOIN Months m ON p.MonthId = m.MonthId
                                                                                                                                                                                                             
    INNER JOIN PlanValues pv ON p.Id = pv.PlanBaseId
                                                                                                                                                                                                         
    WHERE p.PlanType = 'Monthly'
                                                                                                                                                                                                                             
      AND p.IsDeleted = 0
                                                                                                                                                                                                                                    
      AND pv.IsDeleted = 0
                                                                                                                                                                                                                                   
      AND pv.MeasurementUnitId = '541EFC25-05D6-4ADA-B2E2-8135E0B6FAE0'
                                                                                                                                                                                      
),
                                                                                                                                                                                                                                                           
PlanItemAggregated AS (
                                                                                                                                                                                                                                      
    SELECT 
                                                                                                                                                                                                                                                  
        ParentId, ParentName, ChildId, ChildName,
                                                                                                                                                                                                            
        FiscalYearId, FiscalYearName, IsCurrentFiscalYear,
                                                                                                                                                                                                   
        MonthId, MonthName, MonthOrder,
                                                                                                                                                                                                                      
        BaseData.PlanItemId,
                                                                                                                                                                                                                                 
        pb.CategoryId, pb.CategoryName, pb.CategoryNameAm,
                                                                                                                                                                                                   
        pb.PlanItemName, pb.PlanItemNameAm,
                                                                                                                                                                                                                  
        FiscalYearRank,
                                                                                                                                                                                                                                      
        SUM(Target) AS TotalTarget,
                                                                                                                                                                                                                          
        SUM(Actual) AS TotalActual,
                                                                                                                                                                                                                          
        SUM(BroughtForward) AS TotalBroughtForward,
                                                                                                                                                                                                          
        SUM(TotalTarget) AS GrandTotalTarget,
                                                                                                                                                                                                                
        SUM(CASE WHEN IsSelectedPlanItem = 1 THEN Actual ELSE 0 END) AS TotalSelectedActual,
                                                                                                                                                                 
        SUM(CASE WHEN IsSelectedPlanItem = 1 THEN Target ELSE 0 END) AS TotalSelectedTarget,
                                                                                                                                                                 
        SUM(CASE WHEN IsSelectedPlanItem = 1 THEN Actual ELSE 0 END) AS CurrentSelectedActual,
                                                                                                                                                               
        SUM(CASE WHEN IsSelectedPlanItem = 1 THEN Target ELSE 0 END) AS CurrentSelectedTarget,   -- new
                                                                                                                                                      
        COUNT(*) AS MetricCount,
                                                                                                                                                                                                                             
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
                                                                                                                                                                                                                                            
    INNER JOIN [dbo].[vw_PlanItemsBasic] pb ON BaseData.PlanItemId = pb.PlanItemId
                                                                                                                                                                           
    WHERE pb.CategoryId = 'BE6654B7-FB48-434C-AE4D-9D165F44C209'
                                                                                                                                                                                             
    GROUP BY 
                                                                                                                                                                                                                                                
        ParentId, ParentName, ChildId, ChildName,
                                                                                                                                                                                                            
        FiscalYearId, FiscalYearName, IsCurrentFiscalYear,
                                                                                                                                                                                                   
        MonthId, MonthName, MonthOrder,
                                                                                                                                                                                                                      
        BaseData.PlanItemId,
                                                                                                                                                                                                                                 
        pb.CategoryId, pb.CategoryName, pb.CategoryNameAm,
                                                                                                                                                                                                   
        pb.PlanItemName, pb.PlanItemNameAm,
                                                                                                                                                                                                                  
        FiscalYearRank
                                                                                                                                                                                                                                       
)
                                                                                                                                                                                                                                                            
SELECT 
                                                                                                                                                                                                                                                      
    ParentId, ParentName, ChildId, ChildName,
                                                                                                                                                                                                                
    FiscalYearId, FiscalYearName, IsCurrentFiscalYear,
                                                                                                                                                                                                       
    MonthId, MonthName, MonthOrder,
                                                                                                                                                                                                                          
    PlanItemId, PlanItemName, PlanItemNameAm,
                                                                                                                                                                                                                
    CategoryId, CategoryName, CategoryNameAm,
                                                                                                                                                                                                                
    FiscalYearRank,
                                                                                                                                                                                                                                          
    TotalTarget,
                                                                                                                                                                                                                                             
    TotalActual,
                                                                                                                                                                                                                                             
    TotalBroughtForward,
                                                                                                                                                                                                                                     
    GrandTotalTarget,
                                                                                                                                                                                                                                        
    TotalSelectedActual,
                                                                                                                                                                                                                                     
    TotalSelectedTarget,
                                                                                                                                                                                                                                     
    MetricCount,
                                                                                                                                                                                                                                             
    OverallAchievement,
                                                                                                                                                                                                                                      
    PerformanceStatus,
                                                                                                                                                                                                                                       
    SUM(TotalActual) OVER (
                                                                                                                                                                                                                                  
        PARTITION BY ParentId, ChildId, PlanItemId, FiscalYearId 
                                                                                                                                                                                            
        ORDER BY MonthOrder
                                                                                                                                                                                                                                  
        ROWS UNBOUNDED PRECEDING
                                                                                                                                                                                                                             
    ) AS YTDActual,
                                                                                                                                                                                                                                          
    SUM(GrandTotalTarget) OVER (
                                                                                                                                                                                                                             
        PARTITION BY ParentId, ChildId, PlanItemId, FiscalYearId 
                                                                                                                                                                                            
        ORDER BY MonthOrder
                                                                                                                                                                                                                                  
        ROWS UNBOUNDED PRECEDING
                                                                                                                                                                                                                             
    ) AS YTDTarget,
                                                                                                                                                                                                                                          
    LAG(TotalActual, 12) OVER (
                                                                                                                                                                                                                              
        PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                          
        ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                                  
    ) AS PreviousYearSameMonth,
                                                                                                                                                                                                                              
    LAG(GrandTotalTarget, 12) OVER (
                                                                                                                                                                                                                         
        PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                          
        ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                                  
    ) AS PreviousYearTarget,
                                                                                                                                                                                                                                 
    LAG(CurrentSelectedActual, 12) OVER (
                                                                                                                                                                                                                    
        PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                          
        ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                                  
    ) AS PreviousYearSameMonthSelected,
                                                                                                                                                                                                                      
    -- NEW: previous year's same month selected target
                                                                                                                                                                                                       
    LAG(CurrentSelectedTarget, 12) OVER (
                                                                                                                                                                                                                    
        PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                          
        ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                                  
    ) AS PreviousYearSelectedTarget,
                                                                                                                                                                                                                         
    TotalActual - LAG(TotalActual, 12) OVER (
                                                                                                                                                                                                                
        PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                          
        ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                                  
    ) AS YoYChange,
                                                                                                                                                                                                                                          
    CASE 
                                                                                                                                                                                                                                                    
        WHEN LAG(TotalActual, 12) OVER (
                                                                                                                                                                                                                     
            PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                      
            ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                              
        ) > 0 
                                                                                                                                                                                                                                               
        THEN (TotalActual - LAG(TotalActual, 12) OVER (
                                                                                                                                                                                                      
            PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                      
            ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                              
        )) / LAG(TotalActual, 12) OVER (
                                                                                                                                                                                                                     
            PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                      
            ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                              
        )
                                                                                                                                                                                                                                                    
        ELSE NULL 
                                                                                                                                                                                                                                           
    END AS YoYGrowthPercentage,
                                                                                                                                                                                                                              
    TotalTarget - LAG(TotalTarget, 12) OVER (
                                                                                                                                                                                                                
        PARTITION BY ParentId, ChildId, PlanItemId 
                                                                                                                                                                                                          
        ORDER BY FiscalYearRank, MonthOrder
                                                                                                                                                                                                                  
    ) AS TargetVariance
                                                                                                                                                                                                                                      
FROM PlanItemAggregated;
                                                                                                                                                                                                                                     
