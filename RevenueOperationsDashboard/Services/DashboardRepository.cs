using Dapper;
using Microsoft.Data.SqlClient;
using RevenueOperationsDashboard.Models;
using RevenueOperationsDashboard.Infrastructure.Time;
using HumanNumbers;

namespace RevenueOperationsDashboard.Services
{
    public class DashboardRepository
    {
        private readonly string _connectionString;
        private readonly IEthiopianDateMapper _dateMapper;
        private readonly IEthiopianClock _clock;
        private readonly IConfiguration _configuration;

        public DashboardRepository(IConfiguration configuration, IEthiopianDateMapper dateMapper, IEthiopianClock clock)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _dateMapper = dateMapper;
            _clock = clock;
        }

        private async Task<SqlConnection> GetConnectionAsync()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        private string? NormalizeFilter(string? val) =>
            (string.IsNullOrEmpty(val) || val.Equals("ALL", StringComparison.OrdinalIgnoreCase)) ? null : val;

        public (DateTime? StartDate, DateTime? EndDate) ResolveDateRange(DashboardFilterDto filters)
        {
            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(filters.StartDate) || !string.IsNullOrWhiteSpace(filters.EndDate))
            {
                DateTime? s = null;
                DateTime? e = null;
                if (!string.IsNullOrWhiteSpace(filters.StartDate))
                {
                    try
                    {
                        var d = _dateMapper.FromEthiopian(filters.StartDate.Trim());
                        s = d.ToDateTime(TimeOnly.MinValue);
                    }
                    catch { }
                }
                if (!string.IsNullOrWhiteSpace(filters.EndDate))
                {
                    try
                    {
                        var d = _dateMapper.FromEthiopian(filters.EndDate.Trim());
                        e = d.ToDateTime(new TimeOnly(23, 59, 59));
                    }
                    catch { }
                }
                return (s, e);
            }

            if (preset == "TODAY")
            {
                var todayEt = _clock.Today();
                var startG = _clock.ToGregorian(todayEt.Year, todayEt.Month, todayEt.Day).Date;
                var endG = startG.AddDays(1).AddTicks(-1);
                return (startG, endG);
            }

            if (preset == "THIS_MONTH")
            {
                var todayEt = _clock.Today();
                var startG = _clock.ToGregorian(todayEt.Year, todayEt.Month, 1).Date;
                int lastDay = todayEt.Month == 13 ? (todayEt.Year % 4 == 3 ? 6 : 5) : 30;
                var endG = _clock.ToGregorian(todayEt.Year, todayEt.Month, lastDay).Date.AddDays(1).AddTicks(-1);
                return (startG, endG);
            }

            int year = 2018;
            if (int.TryParse(filters.FiscalYearId, out var fyNum))
            {
                year = fyNum;
            }
            else
            {
                year = _clock.Today().Year;
            }

            if (preset == "Q1") // Hamle (11), Nehase (12), Meskerem (1) -> MonthOrder 1-3
            {
                var s = _clock.ToGregorian(year - 1, 11, 1).Date;
                var e = _clock.ToGregorian(year, 1, 30).Date.AddDays(1).AddTicks(-1);
                return (s, e);
            }
            if (preset == "Q2") // Tikimt (2), Hidar (3), Tahsas (4) -> MonthOrder 4-6
            {
                var s = _clock.ToGregorian(year, 2, 1).Date;
                var e = _clock.ToGregorian(year, 4, 30).Date.AddDays(1).AddTicks(-1);
                return (s, e);
            }
            if (preset == "Q3") // Tir (5), Yekatit (6), Megabit (7) -> MonthOrder 7-9
            {
                var s = _clock.ToGregorian(year, 5, 1).Date;
                var e = _clock.ToGregorian(year, 7, 30).Date.AddDays(1).AddTicks(-1);
                return (s, e);
            }
            if (preset == "Q4") // Miyazya (8), Ginbot (9), Sene (10) -> MonthOrder 10-12
            {
                var s = _clock.ToGregorian(year, 8, 1).Date;
                var e = _clock.ToGregorian(year, 10, 30).Date.AddDays(1).AddTicks(-1);
                return (s, e);
            }

            return (null, null);
        }

        // 1. Tax Type Performance (Billions ETB)  
        public async Task<TaxTypePerformanceDto> GetTaxTypePerformanceAsync(DashboardFilterDto filters)
        {
            var (startDate, endDate) = ResolveDateRange(filters);
            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            bool isQuarter = preset is "Q1" or "Q2" or "Q3" or "Q4";

            string sql;
            if (isQuarter)
            {
                sql = @"  
                    SELECT   
                        PlanItemName,  
                        ROUND(SUM(TotalTarget) / 1000000000.0, 2) AS TargetBillions,  
                        ROUND(SUM(TotalActual) / 1000000000.0, 2) AS ActualBillions  
                    FROM [ops].[vw_revenue_performance]  
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                      AND (@ParentId IS NULL OR ParentId = @ParentId)  
                      AND (@PlanItemId IS NULL OR PlanItemId = @PlanItemId)  
                      AND Quarter = @Quarter
                      AND ParentId <> '1'
                      AND PlanItemName IN (
                          N'ምንዳና ደመወዝ(1101)',
                          N'ንግድ ትረፍ(1103)',
                          N'የተጨማሪ እሴት ታክስ ድምር(1120-1199)',
                          N'የኪራይ ገቢ(1102)',
                          N'የማዘጋጃ ቤቶች ገቢ(1700-1799)',
                          N'ቤት ግብር(1701)'
                      )
                    GROUP BY PlanItemName  
                    ORDER BY TargetBillions DESC;";
            }
            else if (startDate.HasValue || endDate.HasValue)
            {
                sql = @"  
                    SELECT   
                        PlanItemName,  
                        ROUND(SUM(TotalMonthlyTarget) / 1000000000.0, 2) AS TargetBillions,  
                        ROUND(SUM(DailyActual) / 1000000000.0, 2) AS ActualBillions  
                    FROM [dbo].[vw_RevenuePerformanceDaily]  
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                      AND (@ParentId IS NULL OR ParentId = @ParentId)  
                      AND (@PlanItemId IS NULL OR PlanItemId = @PlanItemId)
                      AND (@StartDate IS NULL OR EntryDate >= @StartDate)
                      AND (@EndDate IS NULL OR EntryDate <= @EndDate)
                      AND ParentId <> '1'
                      AND PlanItemName IN (
                          N'ምንዳና ደመወዝ(1101)',
                          N'ንግድ ትረፍ(1103)',
                          N'የተጨማሪ እሴት ታክስ ድምር(1120-1199)',
                          N'የኪራይ ገቢ(1102)',
                          N'የማዘጋጃ ቤቶች ገቢ(1700-1799)',
                          N'ቤት ግብር(1701)'
                      )
                    GROUP BY PlanItemName  
                    ORDER BY TargetBillions DESC;";
            }
            else
            {
                sql = @"  
                    SELECT   
                        PlanItemName,  
                        ROUND(SUM(TotalTarget) / 1000000000.0, 2) AS TargetBillions,  
                        ROUND(SUM(TotalActual) / 1000000000.0, 2) AS ActualBillions  
                    FROM [ops].[vw_revenue_performance]  
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                      AND (@ParentId IS NULL OR ParentId = @ParentId)  
                      AND (@PlanItemId IS NULL OR PlanItemId = @PlanItemId)  
                      AND ParentId <> '1'
                      AND PlanItemName IN (
                          N'ምንዳና ደመወዝ(1101)',
                          N'ንግድ ትረፍ(1103)',
                          N'የተጨማሪ እሴት ታክስ ድምር(1120-1199)',
                          N'የኪራይ ገቢ(1102)',
                          N'የማዘጋጃ ቤቶች ገቢ(1700-1799)',
                          N'ቤት ግብር(1701)'
                      )
                    GROUP BY PlanItemName  
                    ORDER BY TargetBillions DESC;";
            }

            using var conn = await GetConnectionAsync();
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
            {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),
                ParentId = NormalizeFilter(filters.ParentId),
                PlanItemId = NormalizeFilter(filters.PlanItemId),
                StartDate = startDate,
                EndDate = endDate,
                Quarter = preset
            }, commandTimeout: 120))).ToList();

            var result = new TaxTypePerformanceDto();
            foreach (var r in rows)
            {
                result.Categories.Add((string)r.PlanItemName);
                result.TargetBillions.Add((decimal)(r.TargetBillions ?? 0m));
                result.ActualBillions.Add((decimal)(r.ActualBillions ?? 0m));
            }
            return result;
        }

        // 2. Monthly Achievement Trend (%)  
        public async Task<MonthlyTrendDto> GetMonthlyTrendAsync(DashboardFilterDto filters)
        {
            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            string? quarter = preset is "Q1" or "Q2" or "Q3" or "Q4" ? preset : null;

            const string sql = @"  
                SELECT   
                    MonthName,  
                    MonthOrder,  
                    CASE   
                        WHEN SUM(CASE 
                            WHEN @PlanItemId IS NULL THEN SelectedTarget
                            ELSE TotalTarget
                        END) > 0 
                        THEN ROUND((SUM(CASE 
                            WHEN @PlanItemId IS NULL THEN SelectedActual
                            ELSE TotalActual
                        END) / SUM(CASE 
                            WHEN @PlanItemId IS NULL THEN SelectedTarget
                            ELSE TotalTarget
                        END)) * 100, 2)
                        ELSE 0 
                    END AS AchievementPct  
                FROM [ops].[vw_revenue_performance]  
                WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                  AND (@ParentId IS NULL OR ParentId = @ParentId)  
                  AND (@Quarter IS NULL OR Quarter = @Quarter)
                  AND (@PlanItemId IS NULL OR PlanItemId = @PlanItemId)
                  AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                GROUP BY MonthName, MonthOrder  
                ORDER BY MonthOrder ASC;";

            using var conn = await GetConnectionAsync();
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
            {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),
                ParentId = NormalizeFilter(filters.ParentId),
                PlanItemId = NormalizeFilter(filters.PlanItemId),
                Quarter = quarter
            }, commandTimeout: 120))).ToList();

            return new MonthlyTrendDto
            {
                Months = rows.Select(r => (string)r.MonthName).ToList(),
                Achievements = rows.Select(r => (decimal)(r.AchievementPct ?? 0m)).ToList()
            };
        }

        public async Task<YoYMonthlyTrendDto> GetYoYMonthlyTrendAsync(DashboardFilterDto filters)
        {
            var currentYearId = NormalizeFilter(filters.FiscalYearId);

            const string sqlPre = @"
                DECLARE @CurrentYearId VARCHAR(100) = @FiscalYearId;
                SELECT TOP 1 
                    CAST(FiscalYearId AS VARCHAR(100)) AS PrevYearId,
                    (SELECT FiscalYearName + N' (በጀት ዓመት)' FROM [dbo].[FiscalYears] WHERE CAST(FiscalYearId AS VARCHAR(100)) = @CurrentYearId) AS CurrentYearLabel,
                    FiscalYearName + N' (በጀት ዓመት)' AS PrevYearLabel
                FROM [dbo].[FiscalYears] 
                WHERE FiscalYearName < (SELECT FiscalYearName FROM [dbo].[FiscalYears] WHERE CAST(FiscalYearId AS VARCHAR(100)) = @CurrentYearId)
                ORDER BY FiscalYearName DESC;";

            using var conn = await GetConnectionAsync();
            var preResult = await conn.QueryFirstOrDefaultAsync<dynamic>(sqlPre, new { FiscalYearId = currentYearId });

            var prevYearId = preResult?.PrevYearId as string;
            var currentYearLabel = preResult?.CurrentYearLabel as string ?? "Current Year";
            var prevYearLabel = preResult?.PrevYearLabel as string ?? "Previous Year";

            // Reuse the highly optimized GetMonthlyTrendAsync query concurrently
            var currentYearTrendTask = GetMonthlyTrendAsync(filters);

            var prevFilters = new DashboardFilterDto
            {
                FiscalYearId = prevYearId,
                ParentId = filters.ParentId,
                PlanItemId = filters.PlanItemId
            };
            var prevYearTrendTask = GetMonthlyTrendAsync(prevFilters);

            await Task.WhenAll(currentYearTrendTask, prevYearTrendTask);

            var currentYearTrend = currentYearTrendTask.Result;
            var prevYearTrend = prevYearTrendTask.Result;

            var result = new YoYMonthlyTrendDto
            {
                Months = currentYearTrend.Months,
                CurrentYearLabel = currentYearLabel,
                PreviousYearLabel = prevYearLabel
            };

            for (int i = 0; i < currentYearTrend.Months.Count; i++)
            {
                result.CurrentYearAchievements.Add(currentYearTrend.Achievements[i]);

                // For previous year, we need to match by month name since arrays might differ (though they shouldn't)
                var monthName = currentYearTrend.Months[i];
                var prevIndex = prevYearTrend.Months.IndexOf(monthName);
                if (prevIndex >= 0)
                {
                    result.PreviousYearAchievements.Add(prevYearTrend.Achievements[prevIndex]);
                }
                else
                {
                    result.PreviousYearAchievements.Add(0m);
                }
            }

            return result;
        }

        // 3. Branch Performance Ranking (%)  
        public async Task<BranchRankingDto> GetBranchRankingAsync(DashboardFilterDto filters)
        {
            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            string? quarter = preset is "Q1" or "Q2" or "Q3" or "Q4" ? preset : null;

            const string sql = @"
                SELECT
                    ParentName AS BranchName,
                    CASE
                        WHEN SUM(SelectedTarget) > 0
                        THEN ROUND(SUM(SelectedActual) / SUM(SelectedTarget) * 100, 2)
                        ELSE 0
                    END AS AchievementPct
                FROM [ops].[vw_revenue_performance]
                WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                  AND (@Quarter IS NULL OR Quarter = @Quarter)
                  AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                  AND MonthId IN (1,2,3,4,5,6,7,8,9,10,11,12)
                GROUP BY ParentId, ParentName  
                ORDER BY AchievementPct ASC;";

            using var conn = await GetConnectionAsync();
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
            {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),
                Quarter = quarter
            }, commandTimeout: 120))).ToList();

            return new BranchRankingDto
            {
                Branches = rows.Select(r => (string)r.BranchName).ToList(),
                Scores = rows.Select(r => (decimal)(r.AchievementPct ?? 0m)).ToList()
            };
        }

        // 4. Dynamic Filter Dropdown Options  
        public async Task<FilterOptionsDto> GetFilterOptionsAsync()
        {
            const string sqlYears = "SELECT CAST(FiscalYearId AS VARCHAR(100)) AS Id, FiscalYearName + N' (በጀት ዓመት)' AS Name FROM [dbo].[FiscalYears] ORDER BY FiscalYearName DESC;";
            const string sqlBranches = "SELECT CAST(ParentId AS VARCHAR(100)) AS Id, ParentName AS Name FROM [dbo].[Parents] WHERE ParentId <> '1' ORDER BY ParentName COLLATE Latin1_General_100_BIN2 ASC;";
            const string sqlTaxTypes = @"
                SELECT DISTINCT CAST(PlanItemId AS VARCHAR(100)) AS Id, PlanItemName AS Name 
                FROM [dbo].[vw_PlanItemsBasic] 
                WHERE PlanItemName IN (
                    N'ምንዳና ደመወዝ(1101)',
                    N'ንግድ ትረፍ(1103)',
                    N'የተጨማሪ እሴት ታክስ ድምር(1120-1199)',
                    N'የኪራይ ገቢ(1102)',
                    N'የማዘጋጃ ቤቶች ገቢ(1700-1799)',
                    N'ቤት ግብር(1701)'
                )
                ORDER BY PlanItemName ASC;";

            using var conn = await GetConnectionAsync();
            var years = await conn.QueryAsync<FilterItem>(sqlYears);
            var branches = await conn.QueryAsync<FilterItem>(sqlBranches);
            var taxTypes = await conn.QueryAsync<FilterItem>(sqlTaxTypes);

            return new FilterOptionsDto
            {
                FiscalYears = years.ToList(),
                Branches = branches.ToList(),
                TaxTypes = taxTypes.ToList()
            };
        }

        // 5. Branch target and actual volumes in Billions ETB
        public async Task<BranchVolumesDto> GetBranchVolumesAsync(DashboardFilterDto filters)
        {
            var (startDate, endDate) = ResolveDateRange(filters);
            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            bool isQuarter = preset is "Q1" or "Q2" or "Q3" or "Q4";

            string sql;
            if (isQuarter)
            {
                sql = @"  
                    SELECT   
                        ParentName AS BranchName,  
                        ROUND(SUM(CASE 
                            WHEN @PlanItemId IS NULL THEN SelectedTarget
                            ELSE TotalTarget
                        END) / 1000000000.0, 2) AS TargetBillions,  
                        ROUND(SUM(CASE 
                            WHEN @PlanItemId IS NULL THEN SelectedActual
                            ELSE TotalActual
                        END) / 1000000000.0, 2) AS ActualBillions  
                    FROM [ops].[vw_revenue_performance]  
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                      AND Quarter = @Quarter
                      AND (@PlanItemId IS NULL OR PlanItemId = @PlanItemId)
                      AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                    GROUP BY ParentId, ParentName  
                    ORDER BY TargetBillions ASC;";
            }
            else if (startDate.HasValue || endDate.HasValue)
            {
                sql = @"  
                    SELECT   
                        ParentName AS BranchName,  
                        ROUND(SUM(CASE 
                            WHEN @PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF') THEN TotalMonthlyTarget
                            WHEN @PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId THEN TotalMonthlyTarget
                            ELSE 0 
                        END) / 1000000000.0, 2) AS TargetBillions,  
                        ROUND(SUM(CASE 
                            WHEN @PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF') THEN DailyActual
                            WHEN @PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId THEN DailyActual
                            ELSE 0 
                        END) / 1000000000.0, 2) AS ActualBillions  
                    FROM [dbo].[vw_RevenuePerformanceDaily]  
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                      AND (@StartDate IS NULL OR EntryDate >= @StartDate)
                      AND (@EndDate IS NULL OR EntryDate <= @EndDate)
                      AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                    GROUP BY ParentId, ParentName  
                    ORDER BY TargetBillions ASC;";
            }
            else
            {
                sql = @"  
                    SELECT   
                        ParentName AS BranchName,  
                        ROUND(SUM(CASE 
                            WHEN @PlanItemId IS NULL THEN SelectedTarget
                            ELSE TotalTarget
                        END) / 1000000000.0, 2) AS TargetBillions,  
                        ROUND(SUM(CASE 
                            WHEN @PlanItemId IS NULL THEN SelectedActual
                            ELSE TotalActual
                        END) / 1000000000.0, 2) AS ActualBillions  
                    FROM [ops].[vw_revenue_performance]  
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                      AND (@PlanItemId IS NULL OR PlanItemId = @PlanItemId)
                      AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                    GROUP BY ParentId, ParentName  
                    ORDER BY TargetBillions ASC;";
            }

            using var conn = await GetConnectionAsync();
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
            {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),
                PlanItemId = NormalizeFilter(filters.PlanItemId),
                StartDate = startDate,
                EndDate = endDate,
                Quarter = preset
            }, commandTimeout: 120))).ToList();

            var result = new BranchVolumesDto();
            foreach (var r in rows)
            {
                result.Branches.Add((string)r.BranchName ?? "Unknown");
                result.TargetBillions.Add((decimal)(r.TargetBillions ?? 0m));
                result.ActualBillions.Add((decimal)(r.ActualBillions ?? 0m));
            }
            return result;
        }

        // 6. Top 7 KPI Cards (Parallelized queries across SQL Views with configurable Run-Rate vs YTD)
        public async Task<TopCardsResponseDto> GetTopCardsAsync(DashboardFilterDto filters)
        {
            var fy = NormalizeFilter(filters.FiscalYearId) ?? "2018";
            var parentId = NormalizeFilter(filters.ParentId);
            var (startDate, endDate) = ResolveDateRange(filters);

            var defaultMode = _configuration.GetValue<string>("DashboardSettings:KpiCalculationMode") ?? "MonthlyRunRate";
            var activeMode = string.Equals(filters.CalculationMode, "CumulativeYtd", StringComparison.OrdinalIgnoreCase)
                ? "CumulativeYtd"
                : (string.Equals(filters.CalculationMode, "MonthlyRunRate", StringComparison.OrdinalIgnoreCase) ? "MonthlyRunRate" : defaultMode);

            bool isRunRate = string.Equals(activeMode, "MonthlyRunRate", StringComparison.OrdinalIgnoreCase);

            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            bool isQuarter = preset is "Q1" or "Q2" or "Q3" or "Q4";

            var task1 = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                if (isQuarter)
                {
                    const string sqlCard1Quarter = @"
                        SELECT 
                            SUM(SelectedActual) AS Actual,
                            SUM(SelectedTarget) AS Target,
                            COUNT(DISTINCT CASE WHEN SelectedActual > 0 THEN MonthId END) AS ActiveMonths
                        FROM [ops].[vw_revenue_performance]
                        WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                          AND (@ParentId IS NULL OR ParentId = @ParentId)
                          AND Quarter = @Quarter
                          AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68);";
                    return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard1Quarter, new { FiscalYearId = fy, ParentId = parentId, Quarter = preset }, commandTimeout: 120));
                }

                if (startDate.HasValue || endDate.HasValue)
                {
                    const string sqlCard1Date = @"
                        SELECT 
                            SUM(DailyActual) AS Actual,
                            SUM(TotalMonthlyTarget) AS Target,
                            COUNT(DISTINCT MonthId) AS ActiveMonths
                        FROM [dbo].[vw_RevenuePerformanceDaily]
                        WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                          AND (@ParentId IS NULL OR ParentId = @ParentId)
                          AND (@StartDate IS NULL OR EntryDate >= @StartDate)
                          AND (@EndDate IS NULL OR EntryDate <= @EndDate)
                          AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68);";
                    return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard1Date, new { FiscalYearId = fy, ParentId = parentId, StartDate = startDate, EndDate = endDate }, commandTimeout: 120));
                }

                const string sqlCard1 = @"
                    SELECT 
                        SUM(SelectedActual) AS Actual,
                        SUM(SelectedTarget) AS Target,
                        COUNT(DISTINCT CASE WHEN SelectedActual > 0 THEN MonthId END) AS ActiveMonths
                    FROM [ops].[vw_revenue_performance]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND MonthId IN (1,2,3,4,5,6,7,8,9,10,11,12)
                      AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68);";
                return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard1, new { FiscalYearId = fy, ParentId = parentId }, commandTimeout: 120));
            });

            // Card 2: Annual Filing (Business Profit Tax: 61.49k / 213.49k = 28.8% for FY 2019; 361.34k / 325.23k = 111.1% for FY 2018)
            var task2 = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                if (fy != "2018")
                {
                    const string sqlCard2 = @"
                        SELECT 
                            SUM(ActualProfit) AS Actual, 
                            SUM(TargetProfit) AS Target,
                            COUNT(DISTINCT CASE WHEN ActualProfit > 0 THEN MonthId END) AS ActiveMonths,
                            COUNT(DISTINCT CASE WHEN TargetProfit > 0 THEN MonthId END) AS PlannedMonths
                        FROM [ops].[vw_annual_filers] 
                        WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) AND (@ParentId IS NULL OR ParentId = @ParentId);";
                    return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard2, new { FiscalYearId = fy, ParentId = parentId }, commandTimeout: 120));
                }
                else
                {
                    const string sqlCard2 = @"
                        SELECT 
                            SUM(Declared_Total/2) AS Actual, 
                            SUM(Expected_Total/2) AS Target,
                            COUNT(DISTINCT CASE WHEN Declared_Total > 0 THEN MonthId END) AS ActiveMonths,
                            COUNT(DISTINCT CASE WHEN Expected_Total > 0 THEN MonthId END) AS PlannedMonths
                        FROM [dbo].[vw_AnnualFiler_Short] 
                        WHERE FiscalYearId = @FiscalYearId AND (@ParentId IS NULL OR ParentId = @ParentId);";
                    return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard2, new { FiscalYearId = fy, ParentId = parentId }, commandTimeout: 120));
                }
            });

            var task3 = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                const string sqlCard3 = @"
                    SELECT 
                        SUM(Actual) AS Actual, 
                        SUM(Target) AS Target,
                        COUNT(DISTINCT CASE WHEN Actual > 0 THEN MonthId END) AS ActiveMonths,
                        COUNT(DISTINCT CASE WHEN Target > 0 THEN MonthId END) AS PlannedMonths
                    FROM [dbo].[vw_RenwalClearance] 
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) AND (@ParentId IS NULL OR ParentId = @ParentId);";
                return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard3, new { FiscalYearId = fy, ParentId = parentId }, commandTimeout: 120));
            });

            var task4 = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                const string sqlCard4 = @"
                    SELECT 
                        SUM(Actual) AS Actual, 
                        SUM(Target) AS Target,
                        COUNT(DISTINCT CASE WHEN Actual > 0 THEN MonthId END) AS ActiveMonths,
                        COUNT(DISTINCT CASE WHEN Target > 0 THEN MonthId END) AS PlannedMonths
                    FROM [ops].[vw_vat_filers] 
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) AND (@ParentId IS NULL OR ParentId = @ParentId);";
                return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard4, new { FiscalYearId = fy, ParentId = parentId }, commandTimeout: 120));
            });

            var task5 = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                const string sqlCard5 = @"
                    SELECT 
                        SUM(Actual) AS Actual, 
                        SUM(Target) AS Target,
                        COUNT(DISTINCT CASE WHEN Actual > 0 THEN MonthId END) AS ActiveMonths,
                        COUNT(DISTINCT CASE WHEN Target > 0 THEN MonthId END) AS PlannedMonths
                    FROM [ops].[vw_incometax_filers] 
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) AND (@ParentId IS NULL OR ParentId = @ParentId);";
                return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard5, new { FiscalYearId = fy, ParentId = parentId }, commandTimeout: 120));
            });

            var task6 = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                const string sqlCard6 = @"SELECT SUM(DebtcollectionperformanceCurrency_Actual) AS Actual, SUM(DebtcollectionperformanceCurrency_Target) AS Target FROM [dbo].[vw_DebtCollection] WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) AND (@ParentId IS NULL OR ParentId = @ParentId);";
                return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard6, new { FiscalYearId = fy, ParentId = parentId }, commandTimeout: 120));
            });

            var task7 = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                const string sqlCard7 = @"
                    SELECT 
                        SUM(File_Total_Actual) AS Actual, 
                        SUM(File_Total_Target) AS Target,
                        COUNT(DISTINCT CASE WHEN File_Total_Actual > 0 THEN MonthId END) AS ActiveMonths,
                        COUNT(DISTINCT CASE WHEN File_Total_Target > 0 THEN MonthId END) AS PlannedMonths
                    FROM [dbo].[vw_TaxAudit] 
                    WHERE (@FiscalYearId IS NULL OR FiscalYearName = @FiscalYearId) AND (@ParentId IS NULL OR BranchName = @ParentId);";
                return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard7, new { FiscalYearId = fy, ParentId = parentId }, commandTimeout: 120));
            });

            var taskIsCurrentFy = Task.Run(async () =>
            {
                try
                {
                    using var conn = await GetConnectionAsync();
                    const string sqlFy = @"SELECT TOP 1 IsCurrent FROM [dbo].[FiscalYears] WHERE FiscalYearId = @FiscalYearId;";
                    var isCur = await conn.QueryFirstOrDefaultAsync<bool?>(new CommandDefinition(sqlFy, new { FiscalYearId = fy }, commandTimeout: 30));
                    return isCur ?? (fy == "2019");
                }
                catch
                {
                    return fy == "2019";
                }
            });

            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7, taskIsCurrentFy);

            var card1Row = await task1;
            var card2Row = await task2;
            var card3Row = await task3;
            var card4Row = await task4;
            var card5Row = await task5;
            var card6Row = await task6;
            var card7Row = await task7;
            var isCurrentFy = await taskIsCurrentFy;

            // Card 1: Revenue (Pure operational plan from ops.vw_revenue_performance)
            decimal c1Act = (decimal)(card1Row?.Actual ?? 0m);
            decimal c1Tgt = (decimal)(card1Row?.Target ?? 0m);

            if (isCurrentFy && isRunRate)
            {
                int revMonths = Math.Max(1, Convert.ToInt32(card1Row?.ActiveMonths ?? 1));
                c1Act = Math.Round(c1Act / revMonths, 2);
                c1Tgt = Math.Round(c1Tgt / 12m, 2); // 12-month standard monthly run-rate target
            }
            else if (!isCurrentFy && isRunRate && isQuarter)
            {
                c1Act = Math.Round(c1Act / 3m, 2);
                c1Tgt = Math.Round(c1Tgt / 3m, 2);
            }
            else if (!isCurrentFy && isRunRate && !isQuarter)
            {
                c1Act = Math.Round(c1Act / 12m, 2);
                c1Tgt = Math.Round(c1Tgt / 12m, 2);
            }
            decimal c1Pct = c1Tgt > 0 ? Math.Round((c1Act / c1Tgt) * 100m, 1) : 0m;

            // Card 2: Annual Filing (Always YTD Annual Progress)
            decimal c2Act = (decimal)(card2Row?.Actual ?? 0m);
            decimal c2Tgt = (decimal)(card2Row?.Target ?? 0m);
            decimal c2Pct = c2Tgt > 0 ? Math.Round((c2Act / c2Tgt) * 100m, 1) : 0m;

            // Card 3: Renewal & Clearance
            decimal c3Act = (decimal)(card3Row?.Actual ?? 0m);
            decimal c3Tgt = (decimal)(card3Row?.Target ?? 0m);
            if (isCurrentFy && isRunRate)
            {
                int actM = Math.Max(1, Convert.ToInt32(card3Row?.ActiveMonths ?? 1));
                int tgtM = Math.Max(1, Convert.ToInt32(card3Row?.PlannedMonths ?? 1));
                c3Act = Math.Round(c3Act / actM, 0);
                c3Tgt = Math.Round(c3Tgt / tgtM, 0);
            }
            decimal c3Pct = c3Tgt > 0 ? Math.Round((c3Act / c3Tgt) * 100m, 1) : 0m;

            // Card 4: VAT Declarations (Always monthly average of recurring filing compliance)
            decimal c4Act = (decimal)(card4Row?.Actual ?? 0m);
            decimal c4Tgt = (decimal)(card4Row?.Target ?? 0m);
            if (!isCurrentFy)
            {
                // For past closed fiscal years, standard monthly average across the 12 months
                c4Act = Math.Round(c4Act / 12m, 0);
                c4Tgt = Math.Round(c4Tgt / 12m, 0);
            }
            else
            {
                int actM = Math.Max(1, Convert.ToInt32(card4Row?.ActiveMonths ?? 1));
                int tgtM = Math.Max(1, Convert.ToInt32(card4Row?.PlannedMonths ?? 1));
                c4Act = Math.Round(c4Act / actM, 0);
                c4Tgt = Math.Round(c4Tgt / tgtM, 0);
            }
            decimal c4Pct = c4Tgt > 0 ? Math.Round((c4Act / c4Tgt) * 100m, 2) : 0m;

            // Card 5: Payroll Income Tax (Always monthly average of recurring filing compliance)
            decimal c5Act = (decimal)(card5Row?.Actual ?? 0m);
            decimal c5Tgt = (decimal)(card5Row?.Target ?? 0m);
            if (!isCurrentFy)
            {
                // For past closed fiscal years, standard monthly average across the 12 months
                c5Act = Math.Round(c5Act / 12m, 0);
                c5Tgt = Math.Round(c5Tgt / 12m, 0);
            }
            else
            {
                int actM = Math.Max(1, Convert.ToInt32(card5Row?.ActiveMonths ?? 1));
                int tgtM = Math.Max(1, Convert.ToInt32(card5Row?.PlannedMonths ?? 1));
                c5Act = Math.Round(c5Act / actM, 0);
                c5Tgt = Math.Round(c5Tgt / tgtM, 0);
            }
            decimal c5Pct = c5Tgt > 0 ? Math.Round((c5Act / c5Tgt) * 100m, 2) : 0m;

            decimal c6Act = (decimal)(card6Row?.Actual ?? 0m);
            decimal c6Tgt = (decimal)(card6Row?.Target ?? 0m);
            decimal c6Pct = c6Tgt > 0 ? Math.Round((c6Act / c6Tgt) * 100m, 2) : 0m;

            // Card 7: Tax Audit Performance (Always Cumulative YTD)
            decimal c7Act = (decimal)(card7Row?.Actual ?? 0m);
            decimal c7Tgt = (decimal)(card7Row?.Target ?? 0m);
            decimal c7Pct = c7Tgt > 0 ? Math.Round((c7Act / c7Tgt) * 100m, 1) : 0m;

            var isEnglish = string.Equals(filters.Lang, "en", StringComparison.OrdinalIgnoreCase);
            var currencyPolicy = isEnglish ? "EnglishCurrency" : "AmharicCurrency";
            var countPolicy = isEnglish ? "EnglishCount" : "AmharicCount";

            var currencyOptions = HumanNumbersConfig.Instance.GetPolicies().TryGetValue(currencyPolicy, out var cp) ? cp : HumanNumbersConfig.Instance.GlobalOptions;
            var countOptions = HumanNumbersConfig.Instance.GetPolicies().TryGetValue(countPolicy, out var ctp) ? ctp : HumanNumbersConfig.Instance.GlobalOptions;

            string FormatAmount(decimal val, bool isCurrency = true)
            {
                return isCurrency ? val.ToHumanCurrency(currencyOptions) : val.ToHuman(countOptions);
            }

            string runRateSubAm = "ወርሃዊ አማካይ";
            string runRateSubEn = "Monthly Run-Rate";
            string ytdSubAm = "ዓመታዊ ድምር";
            string ytdSubEn = "Cumulative YTD";

            string cardSubtitleAm(string cardId)
            {
                if (cardId == "kpi2" || cardId == "kpi7") return "ዓመታዊ ድምር";
                if (cardId == "kpi4" || cardId == "kpi5") return "ወርሃዊ አማካይ";
                if (!isCurrentFy) return "ዓመታዊ ድምር";
                return isRunRate ? runRateSubAm : ytdSubAm;
            }

            string cardSubtitleEn(string cardId)
            {
                if (cardId == "kpi2" || cardId == "kpi7") return "Cumulative YTD";
                if (cardId == "kpi4" || cardId == "kpi5") return "Monthly Average";
                if (!isCurrentFy) return "Annual Total";
                return isRunRate ? runRateSubEn : ytdSubEn;
            }

            return new TopCardsResponseDto
            {
                ActiveCalculationMode = activeMode,
                Cards = new List<TopKpiCardItemDto>
                {
                    new TopKpiCardItemDto
                    {
                        Id = "kpi1",
                        TitleAmharic = "ገቢ አፈፃፀም",
                        TitleEnglish = "Revenue Achievement",
                        SubtitleAmharic = cardSubtitleAm("kpi1"),
                        SubtitleEnglish = cardSubtitleEn("kpi1"),
                        CalculationMode = activeMode,
                        Actual = c1Act,
                        Target = c1Tgt,
                        AchievementPct = c1Pct,
                        Unit = "%",
                        FormattedActual = $"{c1Pct}%",
                        FormattedTarget = $"{FormatAmount(c1Act, true)} ({FormatAmount(c1Tgt, true)})"
                    },
                    new TopKpiCardItemDto
                    {
                        Id = "kpi2",
                        TitleAmharic = "ዓመታዊ ገቢ ማሳወቅ",
                        TitleEnglish = "Annual Filing",
                        SubtitleAmharic = cardSubtitleAm("kpi2"),
                        SubtitleEnglish = cardSubtitleEn("kpi2"),
                        CalculationMode = activeMode,
                        Actual = c2Act,
                        Target = c2Tgt,
                        AchievementPct = c2Pct,
                        Unit = "Count",
                        FormattedActual = FormatAmount(c2Act, false),
                        FormattedTarget = $"{FormatAmount(c2Tgt, false)}"
                    },
                    new TopKpiCardItemDto
                    {
                        Id = "kpi3",
                        TitleAmharic = "ማደሻ ክሊራንስ የወሰዱ",
                        TitleEnglish = "Renewal Clearances",
                        SubtitleAmharic = cardSubtitleAm("kpi3"),
                        SubtitleEnglish = cardSubtitleEn("kpi3"),
                        CalculationMode = activeMode,
                        Actual = c3Act,
                        Target = c3Tgt,
                        AchievementPct = c3Pct,
                        Unit = "Count",
                        FormattedActual = FormatAmount(c3Act, false),
                        FormattedTarget = $"{FormatAmount(c3Tgt, false)}"
                    },
                    new TopKpiCardItemDto
                    {
                        Id = "kpi4",
                        TitleAmharic = "ቫት ያሳወቁ",
                        TitleEnglish = "VAT Declarations",
                        SubtitleAmharic = cardSubtitleAm("kpi4"),
                        SubtitleEnglish = cardSubtitleEn("kpi4"),
                        CalculationMode = activeMode,
                        Actual = c4Act,
                        Target = c4Tgt,
                        AchievementPct = c4Pct,
                        Unit = "Count",
                        FormattedActual = FormatAmount(c4Act, false),
                        FormattedTarget = $"{FormatAmount(c4Tgt, false)}"
                    },
                    new TopKpiCardItemDto
                    {
                        Id = "kpi5",
                        TitleAmharic = "የደመወዝ ገቢ ግብር",
                        TitleEnglish = "Payroll Income Tax",
                        SubtitleAmharic = cardSubtitleAm("kpi5"),
                        SubtitleEnglish = cardSubtitleEn("kpi5"),
                        CalculationMode = activeMode,
                        Actual = c5Act,
                        Target = c5Tgt,
                        AchievementPct = c5Pct,
                        Unit = "Count",
                        FormattedActual = FormatAmount(c5Act, false),
                        FormattedTarget = $"{FormatAmount(c5Tgt, false)}"
                    },
                    new TopKpiCardItemDto
                    {
                        Id = "kpi7",
                        TitleAmharic = "ታክስ ኦዲት ያሳወቁ",
                        TitleEnglish = "Tax Audit Performance",
                        SubtitleAmharic = cardSubtitleAm("kpi7"),
                        SubtitleEnglish = cardSubtitleEn("kpi7"),
                        CalculationMode = activeMode,
                        Actual = c7Act,
                        Target = c7Tgt,
                        AchievementPct = c7Pct,
                        Unit = "Count",
                        FormattedActual = FormatAmount(c7Act, false),
                        FormattedTarget = $"{FormatAmount(c7Tgt, false)}"
                    }
                }
            };
        }

        // 7. Daily Revenue Trend (Billions ETB with Ethiopian date labels and daily target run-rate)
        public async Task<DailyTrendDto> GetDailyTrendAsync(DashboardFilterDto filters)
        {
            var (startDate, endDate) = ResolveDateRange(filters);

            const string sql = @"
                WITH DistinctMonthlyTargets AS (
                    SELECT DISTINCT
                        ParentId,
                        MonthId,
                        PlanItemId,
                        TotalMonthlyTarget
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND (
                          (@PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF'))
                          OR (@PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId)
                      )
                ),
                DailyTargetsByMonth AS (
                    SELECT 
                        MonthId,
                        ROUND((SUM(TotalMonthlyTarget) / 30.0) / 1000000000.0, 4) AS DailyTargetRunRateBillions
                    FROM DistinctMonthlyTargets
                    GROUP BY MonthId
                ),
                DailyActuals AS (
                    SELECT 
                        EntryDate,
                        MonthId,
                        ROUND(SUM(DailyActual) / 1000000000.0, 4) AS ActualBillions
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND (
                          (@PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF'))
                          OR (@PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId)
                      )
                      AND (@StartDate IS NULL OR EntryDate >= @StartDate)
                      AND (@EndDate IS NULL OR EntryDate <= @EndDate)
                      AND EntryDate IS NOT NULL
                    GROUP BY EntryDate, MonthId
                )
                SELECT 
                    da.EntryDate,
                    SUM(da.ActualBillions) AS ActualBillions,
                    MAX(ISNULL(dt.DailyTargetRunRateBillions, 0)) AS TargetBillions
                FROM DailyActuals da
                LEFT JOIN DailyTargetsByMonth dt ON da.MonthId = dt.MonthId
                GROUP BY da.EntryDate
                ORDER BY da.EntryDate ASC;";

            using var conn = await GetConnectionAsync();
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
            {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),
                ParentId = NormalizeFilter(filters.ParentId),
                PlanItemId = NormalizeFilter(filters.PlanItemId),
                StartDate = startDate,
                EndDate = endDate
            }, commandTimeout: 120))).ToList();

            var result = new DailyTrendDto();
            foreach (var r in rows)
            {
                DateTime entryDt = (DateTime)r.EntryDate;
                var etDate = _dateMapper.ToEthiopian(DateOnly.FromDateTime(entryDt));
                result.Dates.Add(etDate);
                result.ActualBillions.Add((decimal)(r.ActualBillions ?? 0m));
                result.TargetBillions.Add((decimal)(r.TargetBillions ?? 0m));
            }

            return result;
        }
    }
}
