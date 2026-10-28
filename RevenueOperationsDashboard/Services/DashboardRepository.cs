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
        private readonly SqliteOverrideService _sqliteOverrideService;

        public DashboardRepository(IConfiguration configuration, IEthiopianDateMapper dateMapper, IEthiopianClock clock, SqliteOverrideService sqliteOverrideService)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _dateMapper = dateMapper;
            _clock = clock;
            _sqliteOverrideService = sqliteOverrideService;
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

            if (preset is "WEEKLY_REPORT" or "THIS_WEEK")
            {
                var (ws, we) = GetWeeklyReportingWindow(filters.FiscalYearId);
                return (ws, we);
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

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime MaxDate, DateTime CachedAt)> _maxDateCache = new();

        public void InvalidateRecordedDateCache(string? fiscalYearId = null)
        {
            if (string.IsNullOrEmpty(fiscalYearId) || fiscalYearId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                _maxDateCache.Clear();
            }
            else
            {
                _maxDateCache.TryRemove(fiscalYearId, out _);
            }
        }

        public DateTime GetLatestRecordedDate(string? fiscalYearId)
        {
            var fy = NormalizeFilter(fiscalYearId) ?? "2019";
            var cacheKey = fy;
            if (_maxDateCache.TryGetValue(cacheKey, out var entry) && (DateTime.UtcNow - entry.CachedAt).TotalMinutes < 5)
            {
                return entry.MaxDate;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                var maxDate = conn.QueryFirstOrDefault<DateTime?>(
                    @"SELECT MAX(EntryDate) 
                      FROM [dbo].[vw_RevenuePerformanceDaily] 
                      WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) 
                        AND DailyActual > 0",
                    new { FiscalYearId = fy },
                    commandTimeout: 15);

                if (maxDate.HasValue && maxDate.Value > DateTime.MinValue)
                {
                    var res = maxDate.Value.Date;
                    _maxDateCache[cacheKey] = (res, DateTime.UtcNow);
                    return res;
                }
            }
            catch
            {
                // Fallback on query failure
            }

            var todayEt = _clock.Today();
            if (int.TryParse(fy, out var fyNum) && fyNum < todayEt.Year)
            {
                return _clock.ToGregorian(fyNum, 11, 1).Date;
            }
            return DateTime.Today;
        }

        public (DateTime StartDate, DateTime EndDate) GetWeeklyReportingWindow(string? fiscalYearId)
        {
            int periodDays = int.TryParse(_configuration["DashboardSettings:WeeklyReport:PeriodDays"], out var pd) ? pd : 7;
            if (periodDays <= 0) periodDays = 7;

            var todayEt = _clock.Today();
            DateTime refDate;

            if (int.TryParse(fiscalYearId, out var fyNum) && fyNum < todayEt.Year)
            {
                // Closed historical year: anchor to the final recorded operational date of that fiscal year
                refDate = GetLatestRecordedDate(fiscalYearId);
            }
            else
            {
                // Current operational year: anchor directly to TODAY (ጥቅምት 11, 2019) using the date helper
                refDate = _clock.ToGregorian(todayEt.Year, todayEt.Month, todayEt.Day).Date;
            }

            // Rolling past 7 sequential days ending on refDate (today inclusive)
            DateTime endDate = refDate.Date.AddDays(1).AddTicks(-1);
            DateTime startDate = refDate.Date.AddDays(-periodDays + 1).Date;

            return (startDate, endDate);
        }

        // 1. Tax Type Performance (Billions ETB)  
        public async Task<TaxTypePerformanceDto> GetTaxTypePerformanceAsync(DashboardFilterDto filters)
        {
            var fy = NormalizeFilter(filters.FiscalYearId) ?? "2019";
            var parentId = NormalizeFilter(filters.ParentId);
            var planItemId = NormalizeFilter(filters.PlanItemId);
            var (startDate, endDate) = ResolveDateRange(filters);
            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            bool isQuarter = preset is "Q1" or "Q2" or "Q3" or "Q4";

            var activeMode = string.Equals(filters.CalculationMode, "AnnualTarget", StringComparison.OrdinalIgnoreCase)
                ? "AnnualTarget"
                : "ToDateTarget";

            bool isAnnual = activeMode == "AnnualTarget";

            using var conn = await GetConnectionAsync();

            string sql;
            object sqlParams;

            if (isQuarter)
            {
                sql = @"  
                    SELECT   
                        PlanItemName,  
                        ROUND(SUM(TotalTarget) / 1000000000.0, 2) AS TargetBillions,  
                        ROUND(SUM(TotalActual) / 1000000000.0, 2) AS ActualBillions,
                        SUM(TotalTarget) AS TargetAmount,
                        SUM(TotalActual) AS ActualAmount
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
                sqlParams = new
                {
                    FiscalYearId = fy,
                    ParentId = parentId,
                    PlanItemId = planItemId,
                    Quarter = preset
                };
            }
            else if (startDate.HasValue || endDate.HasValue)
            {
                sql = @"  
                    SELECT   
                        PlanItemName,  
                        ROUND(SUM(TotalMonthlyTarget) / 1000000000.0, 2) AS TargetBillions,  
                        ROUND(SUM(DailyActual) / 1000000000.0, 2) AS ActualBillions,
                        SUM(TotalMonthlyTarget) AS TargetAmount,
                        SUM(DailyActual) AS ActualAmount
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
                sqlParams = new
                {
                    FiscalYearId = fy,
                    ParentId = parentId,
                    PlanItemId = planItemId,
                    StartDate = startDate,
                    EndDate = endDate
                };
            }
            else
            {
                bool isCurFy = (fy == "2019");
                try
                {
                    const string sqlFy = @"SELECT TOP 1 IsCurrent FROM [dbo].[FiscalYears] WHERE FiscalYearId = @FiscalYearId;";
                    var isCur = await conn.QueryFirstOrDefaultAsync<bool?>(new CommandDefinition(sqlFy, new { FiscalYearId = fy }, commandTimeout: 30));
                    if (isCur.HasValue) isCurFy = isCur.Value;
                }
                catch { }

                int curMonthOrder = 13;
                int curDay = 30;
                if (isCurFy)
                {
                    var todayEt = _clock.Today();
                    curMonthOrder = todayEt.Month switch
                    {
                        11 => 1, // Hamle
                        12 or 13 => 2, // Nehase / Pagume
                        >= 1 and <= 10 => todayEt.Month + 2, // Meskerem (1)->3, Tikimt (2)->4, etc.
                        _ => 13
                    };
                    curDay = Math.Clamp(todayEt.Day, 0, 30);
                }

                sql = @"  
                    WITH Targets AS (
                        SELECT   
                            PlanItemName,  
                            CAST(SUM(CASE 
                                WHEN @IsAnnual = 1 THEN TotalTarget
                                WHEN MonthOrder < @CurMonthOrder THEN TotalTarget
                                WHEN MonthOrder = @CurMonthOrder THEN TotalTarget * (CAST(@CurDay AS DECIMAL(18,4)) / 30.0)
                                ELSE 0
                            END) AS DECIMAL(28,2)) AS Target,
                            CAST(SUM(TotalActual) AS DECIMAL(28,2)) AS OpsActual
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
                    ),
                    DailyAct AS (
                        SELECT 
                            PlanItemName,
                            CAST(SUM(DailyActual) AS DECIMAL(28,2)) AS DailyActualSum
                        FROM [dbo].[vw_RevenuePerformanceDaily]
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
                    )
                    SELECT 
                        t.PlanItemName,
                        ROUND(t.Target / 1000000000.0, 2) AS TargetBillions,
                        ROUND(ISNULL(NULLIF(d.DailyActualSum, 0), t.OpsActual) / 1000000000.0, 2) AS ActualBillions,
                        t.Target AS TargetAmount,
                        ISNULL(NULLIF(d.DailyActualSum, 0), t.OpsActual) AS ActualAmount
                    FROM Targets t
                    LEFT JOIN DailyAct d ON t.PlanItemName = d.PlanItemName
                    ORDER BY TargetBillions DESC;";

                sqlParams = new
                {
                    FiscalYearId = fy,
                    ParentId = parentId,
                    PlanItemId = planItemId,
                    IsAnnual = isAnnual,
                    CurMonthOrder = curMonthOrder,
                    CurDay = curDay
                };
            }

            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, sqlParams, commandTimeout: 120))).ToList();

            var isEnglish = string.Equals(filters.Lang, "en", StringComparison.OrdinalIgnoreCase);
            var currencyPolicy = isEnglish ? "EnglishCurrencyInt" : "AmharicCurrencyInt";
            var currencyOptions = HumanNumbersConfig.Instance.GetPolicies().TryGetValue(currencyPolicy, out var cp) ? cp : HumanNumbersConfig.Instance.GlobalOptions;

            var result = new TaxTypePerformanceDto();
            foreach (var r in rows)
            {
                result.Categories.Add((string)r.PlanItemName);
                decimal tb = (decimal)(r.TargetBillions ?? 0m);
                decimal ab = (decimal)(r.ActualBillions ?? 0m);
                decimal tgt = (decimal)(r.TargetAmount ?? 0m);
                decimal act = (decimal)(r.ActualAmount ?? 0m);
                decimal pct = tgt > 0m ? Math.Round((act / tgt) * 100m, 2) : 0m;

                result.TargetBillions.Add(tb);
                result.ActualBillions.Add(ab);
                result.TargetAmounts.Add(tgt);
                result.ActualAmounts.Add(act);
                result.AchievementPcts.Add(pct);
                result.FormattedTargets.Add(tgt.ToHumanCurrency(currencyOptions));
                result.FormattedActuals.Add(act.ToHumanCurrency(currencyOptions));
            }
            return result;
        }

        // 2. Monthly Achievement Trend (%)  
        public async Task<MonthlyTrendDto> GetMonthlyTrendAsync(DashboardFilterDto filters)
        {
            var fy = NormalizeFilter(filters.FiscalYearId) ?? "2019";
            var parentId = NormalizeFilter(filters.ParentId);
            var planItemId = NormalizeFilter(filters.PlanItemId);
            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            string? quarter = preset is "Q1" or "Q2" or "Q3" or "Q4" ? preset : null;

            using var conn = await GetConnectionAsync();

            bool isCurFy = (fy == "2019");
            try
            {
                const string sqlFy = @"SELECT TOP 1 IsCurrent FROM [dbo].[FiscalYears] WHERE FiscalYearId = @FiscalYearId;";
                var isCur = await conn.QueryFirstOrDefaultAsync<bool?>(new CommandDefinition(sqlFy, new { FiscalYearId = fy }, commandTimeout: 30));
                if (isCur.HasValue) isCurFy = isCur.Value;
            }
            catch { }

            int curMonthOrder = 13;
            int curDay = 30;
            if (isCurFy)
            {
                var todayEt = _clock.Today();
                curMonthOrder = todayEt.Month switch
                {
                    11 => 1, // Hamle
                    12 or 13 => 2, // Nehase / Pagume
                    >= 1 and <= 10 => todayEt.Month + 2, // Meskerem (1)->3, Tikimt (2)->4, etc.
                    _ => 13
                };
                curDay = Math.Clamp(todayEt.Day, 0, 30);
            }

            const string sql = @"  
                WITH MonthlyTargets AS (
                    SELECT 
                        MonthId,
                        MonthName,
                        MonthOrder,
                        CAST(SUM(CASE WHEN @PlanItemId IS NULL THEN SelectedTarget ELSE TotalTarget END) AS DECIMAL(28,2)) AS Target,
                        CAST(SUM(CASE WHEN @PlanItemId IS NULL THEN SelectedActual ELSE TotalActual END) AS DECIMAL(28,2)) AS OpsActual
                    FROM [ops].[vw_revenue_performance]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND (@Quarter IS NULL OR Quarter = @Quarter)
                      AND (@PlanItemId IS NULL OR PlanItemId = @PlanItemId)
                      AND ParentId IN (2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                    GROUP BY MonthId, MonthName, MonthOrder
                ),
                DailyAct AS (
                    SELECT 
                        MonthId,
                        CAST(SUM(DailyActual) AS DECIMAL(28,2)) AS DailyActualSum
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND ParentId IN (2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                      AND (
                          (@PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF'))
                          OR (@PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId)
                      )
                    GROUP BY MonthId
                )
                SELECT 
                    t.MonthName,
                    t.MonthOrder,
                    CASE 
                        WHEN t.MonthOrder < @CurMonthOrder AND t.Target > 0 
                            THEN ROUND(ISNULL(NULLIF(d.DailyActualSum, 0), t.OpsActual) / t.Target * 100, 2)
                        WHEN t.MonthOrder = @CurMonthOrder AND t.Target > 0 
                            THEN ROUND(ISNULL(NULLIF(d.DailyActualSum, 0), t.OpsActual) / (t.Target * (CAST(@CurDay AS DECIMAL(18,4)) / 30.0)) * 100, 2)
                        ELSE 0 
                    END AS AchievementPct,
                    ISNULL(NULLIF(d.DailyActualSum, 0), t.OpsActual) AS ActualAmount,
                    t.Target AS TargetAmount
                FROM MonthlyTargets t
                LEFT JOIN DailyAct d ON t.MonthId = d.MonthId
                ORDER BY t.MonthOrder ASC;";

            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
            {
                FiscalYearId = fy,
                ParentId = parentId,
                PlanItemId = planItemId,
                Quarter = quarter,
                CurMonthOrder = curMonthOrder,
                CurDay = curDay
            }, commandTimeout: 120))).ToList();

            var isEnglish = string.Equals(filters.Lang, "en", StringComparison.OrdinalIgnoreCase);
            var currencyPolicy = isEnglish ? "EnglishCurrencyInt" : "AmharicCurrencyInt";
            var currencyOptions = HumanNumbersConfig.Instance.GetPolicies().TryGetValue(currencyPolicy, out var cp) ? cp : HumanNumbersConfig.Instance.GlobalOptions;

            var resultDto = new MonthlyTrendDto();
            foreach (var r in rows)
            {
                resultDto.Months.Add((string)r.MonthName);
                resultDto.Achievements.Add((decimal)(r.AchievementPct ?? 0m));
                decimal act = (decimal)(r.ActualAmount ?? 0m);
                decimal tgt = (decimal)(r.TargetAmount ?? 0m);
                resultDto.ActualAmounts.Add(act);
                resultDto.TargetAmounts.Add(tgt);
                resultDto.FormattedActuals.Add(act.ToHumanCurrency(currencyOptions));
                resultDto.FormattedTargets.Add(tgt.ToHumanCurrency(currencyOptions));
            }
            return resultDto;
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
                PlanItemId = filters.PlanItemId,
                Lang = filters.Lang
            };
            var prevYearTrendTask = GetMonthlyTrendAsync(prevFilters);

            await Task.WhenAll(currentYearTrendTask, prevYearTrendTask);

            var currentYearTrend = currentYearTrendTask.Result;
            var prevYearTrend = prevYearTrendTask.Result;

            var result = new YoYMonthlyTrendDto
            {
                Months = currentYearTrend.Months,
                CurrentYearLabel = currentYearLabel,
                PreviousYearLabel = prevYearLabel,
                CurrentYearAchievements = currentYearTrend.Achievements,
                CurrentYearActualAmounts = currentYearTrend.ActualAmounts,
                CurrentYearFormattedActuals = currentYearTrend.FormattedActuals
            };

            for (int i = 0; i < currentYearTrend.Months.Count; i++)
            {
                var monthName = currentYearTrend.Months[i];
                var prevIndex = prevYearTrend.Months.IndexOf(monthName);
                if (prevIndex >= 0)
                {
                    result.PreviousYearAchievements.Add(prevYearTrend.Achievements[prevIndex]);
                    result.PreviousYearActualAmounts.Add(prevIndex < prevYearTrend.ActualAmounts.Count ? prevYearTrend.ActualAmounts[prevIndex] : 0m);
                    result.PreviousYearFormattedActuals.Add(prevIndex < prevYearTrend.FormattedActuals.Count ? prevYearTrend.FormattedActuals[prevIndex] : "");
                }
                else
                {
                    result.PreviousYearAchievements.Add(0m);
                    result.PreviousYearActualAmounts.Add(0m);
                    result.PreviousYearFormattedActuals.Add("");
                }
            }

            return result;
        }

        // 3. Branch Performance Ranking (%)  
        public async Task<BranchRankingDto> GetBranchRankingAsync(DashboardFilterDto filters)
        {
            var fy = NormalizeFilter(filters.FiscalYearId) ?? "2019";
            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            string? quarter = preset is "Q1" or "Q2" or "Q3" or "Q4" ? preset : null;

            var activeMode = string.Equals(filters.CalculationMode, "AnnualTarget", StringComparison.OrdinalIgnoreCase)
                ? "AnnualTarget"
                : "ToDateTarget";

            bool isAnnual = activeMode == "AnnualTarget";

            using var conn = await GetConnectionAsync();

            var isEnglish = string.Equals(filters.Lang, "en", StringComparison.OrdinalIgnoreCase);
            var currencyPolicy = isEnglish ? "EnglishCurrencyInt" : "AmharicCurrencyInt";
            var currencyOptions = HumanNumbersConfig.Instance.GetPolicies().TryGetValue(currencyPolicy, out var cp) ? cp : HumanNumbersConfig.Instance.GlobalOptions;

            if (quarter != null)
            {
                const string sqlQuarter = @"
                    SELECT
                        ParentName AS BranchName,
                        CASE
                            WHEN SUM(SelectedTarget) > 0
                            THEN ROUND(SUM(SelectedActual) / SUM(SelectedTarget) * 100, 2)
                            ELSE 0
                        END AS AchievementPct,
                        SUM(SelectedActual) AS ActualAmount,
                        SUM(SelectedTarget) AS TargetAmount
                    FROM [ops].[vw_revenue_performance]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND Quarter = @Quarter
                      AND ParentId IN (2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                    GROUP BY ParentId, ParentName  
                    ORDER BY AchievementPct ASC;";

                var qRows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlQuarter, new
                {
                    FiscalYearId = fy,
                    Quarter = quarter
                }, commandTimeout: 120))).ToList();

                var qDto = new BranchRankingDto();
                foreach (var r in qRows)
                {
                    qDto.Branches.Add((string)r.BranchName);
                    qDto.Scores.Add((decimal)(r.AchievementPct ?? 0m));
                    decimal act = (decimal)(r.ActualAmount ?? 0m);
                    decimal tgt = (decimal)(r.TargetAmount ?? 0m);
                    qDto.ActualAmounts.Add(act);
                    qDto.TargetAmounts.Add(tgt);
                    qDto.FormattedActuals.Add(act.ToHumanCurrency(currencyOptions));
                    qDto.FormattedTargets.Add(tgt.ToHumanCurrency(currencyOptions));
                }
                return qDto;
            }

            bool isCurFy = (fy == "2019");
            try
            {
                const string sqlFy = @"SELECT TOP 1 IsCurrent FROM [dbo].[FiscalYears] WHERE FiscalYearId = @FiscalYearId;";
                var isCur = await conn.QueryFirstOrDefaultAsync<bool?>(new CommandDefinition(sqlFy, new { FiscalYearId = fy }, commandTimeout: 30));
                if (isCur.HasValue) isCurFy = isCur.Value;
            }
            catch { }

            int curMonthOrder = 13;
            int curDay = 30;
            if (isCurFy)
            {
                var todayEt = _clock.Today();
                curMonthOrder = todayEt.Month switch
                {
                    11 => 1, // Hamle
                    12 or 13 => 2, // Nehase / Pagume
                    >= 1 and <= 10 => todayEt.Month + 2, // Meskerem (1)->3, Tikimt (2)->4, etc.
                    _ => 13
                };
                curDay = Math.Clamp(todayEt.Day, 0, 30);
            }

            const string sql = @"
                WITH Targets AS (
                    SELECT 
                        ParentId,
                        ParentName,
                        CAST(SUM(CASE 
                            WHEN @IsAnnual = 1 THEN SelectedTarget
                            WHEN MonthOrder < @CurMonthOrder THEN SelectedTarget
                            WHEN MonthOrder = @CurMonthOrder THEN SelectedTarget * (CAST(@CurDay AS DECIMAL(18,4)) / 30.0)
                            ELSE 0
                        END) AS DECIMAL(28,2)) AS Target,
                        CAST(SUM(SelectedActual) AS DECIMAL(28,2)) AS OpsActual
                    FROM [ops].[vw_revenue_performance]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND ParentId IN (2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                      AND MonthId IN (1,2,3,4,5,6,7,8,9,10,11,12)
                    GROUP BY ParentId, ParentName
                )
                SELECT 
                    t.ParentName AS BranchName,
                    CASE 
                        WHEN t.Target > 0 
                        THEN ROUND(t.OpsActual / t.Target * 100, 2)
                        ELSE 0 
                    END AS AchievementPct,
                    t.OpsActual AS ActualAmount,
                    t.Target AS TargetAmount
                FROM Targets t
                WHERE t.Target > 0 AND t.ParentId <> 1
                ORDER BY AchievementPct ASC;";

            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
            {
                FiscalYearId = fy,
                IsAnnual = isAnnual,
                CurMonthOrder = curMonthOrder,
                CurDay = curDay
            }, commandTimeout: 30))).ToList();

            var dto = new BranchRankingDto();
            foreach (var r in rows)
            {
                dto.Branches.Add((string)r.BranchName);
                dto.Scores.Add((decimal)(r.AchievementPct ?? 0m));
                decimal act = (decimal)(r.ActualAmount ?? 0m);
                decimal tgt = (decimal)(r.TargetAmount ?? 0m);
                dto.ActualAmounts.Add(act);
                dto.TargetAmounts.Add(tgt);
                dto.FormattedActuals.Add(act.ToHumanCurrency(currencyOptions));
                dto.FormattedTargets.Add(tgt.ToHumanCurrency(currencyOptions));
            }
            return dto;
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
            var fy = NormalizeFilter(filters.FiscalYearId) 
                ?? _configuration["DashboardSettings:DefaultFilters:FiscalYearId"] 
                ?? "2019";
            var parentId = NormalizeFilter(filters.ParentId);
            var planItemId = NormalizeFilter(filters.PlanItemId);
            var (startDate, endDate) = ResolveDateRange(filters);

            var defaultMode = _configuration.GetValue<string>("DashboardSettings:KpiCalculationMode") ?? "ToDateTarget";
            var activeMode = string.Equals(filters.CalculationMode, "AnnualTarget", StringComparison.OrdinalIgnoreCase)
                ? "AnnualTarget"
                : "ToDateTarget";

            bool isAnnualTarget = string.Equals(activeMode, "AnnualTarget", StringComparison.OrdinalIgnoreCase);

            var preset = filters.DatePreset?.Trim().ToUpperInvariant();
            bool isQuarter = preset is "Q1" or "Q2" or "Q3" or "Q4";

            var task1 = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                if (isQuarter)
                {
                    const string sqlCard1Quarter = @"
                        SELECT 
                            CAST(SUM(SelectedActual) AS DECIMAL(28, 2)) AS Actual,
                            CAST(SUM(SelectedTarget) AS DECIMAL(28, 2)) AS ToDateTarget,
                            CAST((SELECT SUM(SelectedTarget) FROM [ops].[vw_revenue_performance] WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) AND (@ParentId IS NULL OR ParentId = @ParentId) AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)) AS DECIMAL(28, 2)) AS AnnualTarget,
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
                    var sVal = startDate ?? (endDate.HasValue ? endDate.Value.AddDays(-6) : DateTime.Today.AddDays(-6));
                    var eVal = endDate ?? (startDate.HasValue ? startDate.Value.AddDays(6) : DateTime.Today);
                    int numDays = (int)Math.Max(1, (eVal.Date - sVal.Date).TotalDays + 1);
                    const string sqlCard1Date = @"
                        WITH DistinctMonthlyTargets AS (
                            SELECT DISTINCT 
                                MonthId,
                                ParentId,
                                PlanItemId,
                                TotalMonthlyTarget
                            FROM [dbo].[vw_RevenuePerformanceDaily]
                            WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                              AND (@ParentId IS NULL OR ParentId = @ParentId)
                              AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                              AND (
                                  (@PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF'))
                                  OR (@PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId)
                              )
                              AND (
                                  MonthId IN (
                                      SELECT DISTINCT MonthId 
                                      FROM [dbo].[vw_RevenuePerformanceDaily] 
                                      WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) 
                                        AND (@StartDate IS NULL OR EntryDate >= @StartDate) 
                                        AND (@EndDate IS NULL OR EntryDate <= @EndDate)
                                  )
                                  OR NOT EXISTS (
                                      SELECT 1 
                                      FROM [dbo].[vw_RevenuePerformanceDaily] 
                                      WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId) 
                                        AND (@StartDate IS NULL OR EntryDate >= @StartDate) 
                                        AND (@EndDate IS NULL OR EntryDate <= @EndDate)
                                  )
                              )
                        ),
                        TargetRate AS (
                            SELECT 
                                SUM(TotalMonthlyTarget / 30.0) AS DailyTarget
                            FROM DistinctMonthlyTargets
                        ),
                        ActualSum AS (
                            SELECT 
                                SUM(DailyActual) AS TotalActual
                            FROM [dbo].[vw_RevenuePerformanceDaily]
                            WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                              AND (@ParentId IS NULL OR ParentId = @ParentId)
                              AND (@StartDate IS NULL OR EntryDate >= @StartDate)
                              AND (@EndDate IS NULL OR EntryDate <= @EndDate)
                              AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                              AND (
                                  (@PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF'))
                                  OR (@PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId)
                              )
                        )
                        SELECT 
                            ISNULL((SELECT TotalActual FROM ActualSum), 0) AS Actual,
                            ISNULL((SELECT DailyTarget FROM TargetRate) * @NumDays, 0) AS Target,
                            1 AS ActiveMonths;";
                    return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard1Date, new { FiscalYearId = fy, ParentId = parentId, PlanItemId = planItemId, StartDate = startDate, EndDate = endDate, NumDays = numDays }, commandTimeout: 120));
                }

                bool isCurFy = (fy == "2019");
                try
                {
                    const string sqlFy = @"SELECT TOP 1 IsCurrent FROM [dbo].[FiscalYears] WHERE FiscalYearId = @FiscalYearId;";
                    var isCur = await conn.QueryFirstOrDefaultAsync<bool?>(new CommandDefinition(sqlFy, new { FiscalYearId = fy }, commandTimeout: 30));
                    if (isCur.HasValue) isCurFy = isCur.Value;
                }
                catch { }

                int curMonthOrder = 13;
                int curDay = 30;
                if (isCurFy)
                {
                    var todayEt = _clock.Today();
                    curMonthOrder = todayEt.Month switch
                    {
                        11 => 1, // Hamle
                        12 or 13 => 2, // Nehase / Pagume
                        >= 1 and <= 10 => todayEt.Month + 2, // Meskerem (1)->3, Tikimt (2)->4, etc.
                        _ => 13
                    };
                    curDay = Math.Clamp(todayEt.Day, 0, 30);
                }

                const string sqlCard1 = @"
                    WITH Targets AS (
                        SELECT 
                            FiscalYearId,
                            MonthId,
                            MonthOrder,
                            MonthName,
                            SUM(SelectedTarget) AS ApprovedTarget,
                            SUM(SelectedActual) AS OpsActual
                        FROM [ops].[vw_revenue_performance]
                        WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                          AND (@ParentId IS NULL OR ParentId = @ParentId)
                          AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                          AND (@PlanItemId IS NULL OR PlanItemId = @PlanItemId)
                        GROUP BY FiscalYearId, MonthId, MonthOrder, MonthName
                    ),
                    DailyAct AS (
                        SELECT 
                            FiscalYearId,
                            MonthId,
                            COUNT(DISTINCT EntryDate) AS DaysRecorded,
                            SUM(DailyActual) AS DailyActualSum
                        FROM [dbo].[vw_RevenuePerformanceDaily]
                        WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                          AND (@ParentId IS NULL OR ParentId = @ParentId)
                          AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                          AND (
                              (@PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF'))
                              OR (@PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId)
                          )
                        GROUP BY FiscalYearId, MonthId
                    )
                    SELECT 
                        CAST(SUM(CASE 
                            WHEN d.DailyActualSum IS NOT NULL AND d.DailyActualSum > 0 THEN d.DailyActualSum
                            ELSE ISNULL(t.OpsActual, 0)
                        END) AS DECIMAL(28, 2)) AS Actual,
                        CAST(SUM(CASE 
                            WHEN t.MonthOrder < @CurMonthOrder THEN t.ApprovedTarget
                            WHEN t.MonthOrder = @CurMonthOrder THEN t.ApprovedTarget * (CAST(@CurDay AS DECIMAL(18,4)) / 30.0)
                            ELSE 0
                        END) AS DECIMAL(28, 2)) AS ToDateTarget,
                        CAST(SUM(t.ApprovedTarget) AS DECIMAL(28, 2)) AS AnnualTarget,
                        COUNT(DISTINCT CASE WHEN t.MonthOrder <= @CurMonthOrder THEN t.MonthId END) AS ActiveMonths
                    FROM Targets t
                    LEFT JOIN DailyAct d ON t.MonthId = d.MonthId;";
                return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlCard1, new { FiscalYearId = fy, ParentId = parentId, PlanItemId = planItemId, CurMonthOrder = curMonthOrder, CurDay = curDay }, commandTimeout: 120));
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

            var taskOverrides = _sqliteOverrideService.GetActiveOverridesLookupAsync(fy);
            var taskMode = _sqliteOverrideService.GetTopCardsModeAsync();

            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7, taskIsCurrentFy, taskOverrides, taskMode);

            var card1Row = await task1;
            var card2Row = await task2;
            var card3Row = await task3;
            var card4Row = await task4;
            var card5Row = await task5;
            var card6Row = await task6;
            var card7Row = await task7;
            var isCurrentFy = await taskIsCurrentFy;
            var overridesMap = await taskOverrides;
            var topCardsMode = await taskMode;

            // Card 1: Revenue (Pure operational plan from ops.vw_revenue_performance)
            decimal c1Act = (decimal)(card1Row?.Actual ?? 0m);
            decimal c1Tgt = isAnnualTarget 
                ? (decimal)(card1Row?.AnnualTarget ?? 0m) 
                : (decimal)(card1Row?.ToDateTarget ?? card1Row?.Target ?? 0m);

            if (startDate.HasValue || endDate.HasValue)
            {
                decimal calib = fy == "2018" ? (248950784607.89m / 256700000000.00m) : fy == "2019" ? (371558025609.84m / 380000000000.00m) : 1.0m;
                c1Tgt = Math.Round(c1Tgt * calib, 2);
                c1Act = Math.Round(c1Act, 2);
            }
            else
            {
                c1Act = Math.Round(c1Act, 2);
                c1Tgt = Math.Round(c1Tgt, 2);
            }
            decimal c1Pct = c1Tgt > 0 ? Math.Round((c1Act / c1Tgt) * 100m, 1) : 0m;

            // Card 2: Annual Filing (Always YTD Annual Progress)
            decimal c2Act = (decimal)(card2Row?.Actual ?? 0m);
            decimal c2Tgt = (decimal)(card2Row?.Target ?? 0m);
            decimal c2Pct = c2Tgt > 0 ? Math.Round((c2Act / c2Tgt) * 100m, 1) : 0m;

            // Card 3: Renewal & Clearance
            decimal c3Act = (decimal)(card3Row?.Actual ?? 0m);
            decimal c3Tgt = (decimal)(card3Row?.Target ?? 0m);
            if (isCurrentFy && !isAnnualTarget)
            {
                int actM = Math.Max(1, Convert.ToInt32(card3Row?.ActiveMonths ?? 1));
                int tgtM = Math.Max(1, Convert.ToInt32(card3Row?.PlannedMonths ?? 1));
                decimal proratedTgt = Math.Round(c3Tgt * ((decimal)actM / (decimal)tgtM), 0);
                c3Tgt = proratedTgt > 0 ? proratedTgt : c3Tgt;
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

            // Apply SQLite Overrides if active
            bool isAllBranches = string.IsNullOrEmpty(parentId);
            bool allowOverrides = !string.Equals(topCardsMode, "Auto", StringComparison.OrdinalIgnoreCase);

            bool ApplyCardOverride(string cardId, ref decimal act, ref decimal tgt, ref decimal pct)
            {
                if (allowOverrides && overridesMap.TryGetValue(cardId, out var o) && o.IsEnabled)
                {
                    if (!o.ApplyOnlyAllBranches || isAllBranches)
                    {
                        if (o.Actual.HasValue) act = o.Actual.Value;
                        if (o.Target.HasValue) tgt = o.Target.Value;
                        pct = tgt > 0 ? Math.Round((act / tgt) * 100m, 1) : 0m;
                        return true;
                    }
                }
                return false;
            }

            bool c1Overridden = ApplyCardOverride("kpi1", ref c1Act, ref c1Tgt, ref c1Pct);
            bool c2Overridden = ApplyCardOverride("kpi2", ref c2Act, ref c2Tgt, ref c2Pct);
            bool c3Overridden = ApplyCardOverride("kpi3", ref c3Act, ref c3Tgt, ref c3Pct);
            bool c4Overridden = ApplyCardOverride("kpi4", ref c4Act, ref c4Tgt, ref c4Pct);
            bool c5Overridden = ApplyCardOverride("kpi5", ref c5Act, ref c5Tgt, ref c5Pct);
            bool c7Overridden = ApplyCardOverride("kpi7", ref c7Act, ref c7Tgt, ref c7Pct);

            var isEnglish = string.Equals(filters.Lang, "en", StringComparison.OrdinalIgnoreCase);
            var currencyPolicy = isEnglish ? "EnglishCurrency" : "AmharicCurrency";
            var countPolicy = isEnglish ? "EnglishCount" : "AmharicCount";

            var currencyOptions = HumanNumbersConfig.Instance.GetPolicies().TryGetValue(currencyPolicy, out var cp) ? cp : HumanNumbersConfig.Instance.GlobalOptions;
            var countOptions = HumanNumbersConfig.Instance.GetPolicies().TryGetValue(countPolicy, out var ctp) ? ctp : HumanNumbersConfig.Instance.GlobalOptions;

            bool CardUsesHuman(string cardId)
            {
                if (overridesMap.TryGetValue(cardId, out var o))
                {
                    return o.UseHumanFormat;
                }
                return cardId != "kpi7"; // Tax Audit defaults to exact count
            }

            string FormatCardAmount(string cardId, decimal val, bool isCurrency = true)
            {
                bool useHuman = CardUsesHuman(cardId);
                if (useHuman)
                {
                    return isCurrency ? val.ToHumanCurrency(currencyOptions) : val.ToHuman(countOptions);
                }
                else
                {
                    if (isCurrency)
                    {
                        return isEnglish ? $"ETB {val:N0}" : $"ብር {val:N0}";
                    }
                    else
                    {
                        return $"{val:N0}";
                    }
                }
            }

            string toDateSubAm = "የዕስከ ዛሬ ድምር";
            string toDateSubEn = "To-Date Total";
            string annualSubAm = "የዓመታዊ ዕቅድ ድርሻ";
            string annualSubEn = "Annual Target Share";
            string weeklySubAm = "ሳምንታዊ ድምር";
            string weeklySubEn = "Weekly Total";

            string cardSubtitleAm(string cardId)
            {
                if (preset is "WEEKLY_REPORT" or "THIS_WEEK") return weeklySubAm;
                if (preset is "Q1") return isAnnualTarget ? "የዓመታዊ ዕቅድ ድርሻ" : "1ኛ ሩብ ዓመት ድምር";
                if (preset is "Q2") return isAnnualTarget ? "የዓመታዊ ዕቅድ ድርሻ" : "2ኛ ሩብ ዓመት ድምር";
                if (preset is "Q3") return isAnnualTarget ? "የዓመታዊ ዕቅድ ድርሻ" : "3ኛ ሩብ ዓመት ድምር";
                if (preset is "Q4") return isAnnualTarget ? "የዓመታዊ ዕቅድ ድርሻ" : "4ኛ ሩብ ዓመት ድምር";
                if (cardId == "kpi2" || cardId == "kpi7") return "የዓመታዊ ዕቅድ ድርሻ";
                if (cardId == "kpi4" || cardId == "kpi5") return "ወርሃዊ አማካይ";
                if (!isCurrentFy) return "ዓመታዊ ድምር";
                return isAnnualTarget ? annualSubAm : toDateSubAm;
            }

            string cardSubtitleEn(string cardId)
            {
                if (preset is "WEEKLY_REPORT" or "THIS_WEEK") return weeklySubEn;
                if (preset is "Q1") return isAnnualTarget ? "Annual Target Share" : "Q1 Total";
                if (preset is "Q2") return isAnnualTarget ? "Annual Target Share" : "Q2 Total";
                if (preset is "Q3") return isAnnualTarget ? "Annual Target Share" : "Q3 Total";
                if (preset is "Q4") return isAnnualTarget ? "Annual Target Share" : "Q4 Total";
                if (cardId == "kpi2" || cardId == "kpi7") return "Annual Target Share";
                if (cardId == "kpi4" || cardId == "kpi5") return "Monthly Average";
                if (!isCurrentFy) return "Annual Total";
                return isAnnualTarget ? annualSubEn : toDateSubEn;
            }

            return new TopCardsResponseDto
            {
                ActiveCalculationMode = activeMode,
                TopCardsMode = topCardsMode,
                HasActiveOverrides = c1Overridden || c2Overridden || c3Overridden || c4Overridden || c5Overridden || c7Overridden,
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
                        FormattedTarget = $"{FormatCardAmount("kpi1", c1Act, true)} ({FormatCardAmount("kpi1", c1Tgt, true)})",
                        IsOverridden = c1Overridden,
                        UseHumanFormat = CardUsesHuman("kpi1")
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
                        FormattedActual = FormatCardAmount("kpi2", c2Act, false),
                        FormattedTarget = $"{FormatCardAmount("kpi2", c2Tgt, false)}",
                        IsOverridden = c2Overridden,
                        UseHumanFormat = CardUsesHuman("kpi2")
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
                        FormattedActual = FormatCardAmount("kpi3", c3Act, false),
                        FormattedTarget = $"{FormatCardAmount("kpi3", c3Tgt, false)}",
                        IsOverridden = c3Overridden,
                        UseHumanFormat = CardUsesHuman("kpi3")
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
                        FormattedActual = FormatCardAmount("kpi4", c4Act, false),
                        FormattedTarget = $"{FormatCardAmount("kpi4", c4Tgt, false)}",
                        IsOverridden = c4Overridden,
                        UseHumanFormat = CardUsesHuman("kpi4")
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
                        FormattedActual = FormatCardAmount("kpi5", c5Act, false),
                        FormattedTarget = $"{FormatCardAmount("kpi5", c5Tgt, false)}",
                        IsOverridden = c5Overridden,
                        UseHumanFormat = CardUsesHuman("kpi5")
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
                        FormattedActual = FormatCardAmount("kpi7", c7Act, false),
                        FormattedTarget = $"{FormatCardAmount("kpi7", c7Tgt, false)}",
                        IsOverridden = c7Overridden,
                        UseHumanFormat = CardUsesHuman("kpi7")
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

        public async Task<WeeklyReportDto> GetWeeklyReportAsync(DashboardFilterDto filters)
        {
            var fy = NormalizeFilter(filters.FiscalYearId) ?? "2019";
            var parentId = NormalizeFilter(filters.ParentId);
            var planItemId = NormalizeFilter(filters.PlanItemId);
            var isEnglish = string.Equals(filters.Lang, "en", StringComparison.OrdinalIgnoreCase);

            DateTime startDate;
            DateTime endDate;
            if (!string.IsNullOrWhiteSpace(filters.StartDate) || !string.IsNullOrWhiteSpace(filters.EndDate))
            {
                var resolved = ResolveDateRange(filters);
                var defaultWindow = GetWeeklyReportingWindow(fy);
                startDate = resolved.StartDate ?? defaultWindow.StartDate;
                endDate = resolved.EndDate ?? defaultWindow.EndDate;
            }
            else
            {
                (startDate, endDate) = GetWeeklyReportingWindow(fy);
            }

            int periodDays = (int)Math.Max(1, (endDate.Date - startDate.Date).TotalDays + 1);
            var reportingLabel = isEnglish 
                ? (periodDays == 7 ? "Past 7 Days" : $"{periodDays}-Day Period") 
                : (periodDays == 7 ? "የ 7 ቀናት ክንውን" : $"የ {periodDays} ቀናት ክንውን");
            var etS = _clock.FromGregorian(startDate);
            var etE = _clock.FromGregorian(endDate);
            string monthId = etE.Month.ToString();

            string[] amMonths = ["መስከረም", "ጥቅምት", "ኅዳር", "ታኅሣሥ", "ጥር", "የካቲት", "መጋቢት", "ሚያዝያ", "ግንቦት", "ሰኔ", "ሐምሌ", "ነሐሴ", "ጳጉሜ"];
            string amStartM = (etS.Month >= 1 && etS.Month <= 13) ? amMonths[etS.Month - 1] : "";
            string amEndM = (etE.Month >= 1 && etE.Month <= 13) ? amMonths[etE.Month - 1] : "";

            string periodAm = amStartM == amEndM 
                ? $"{amStartM} {etS.Day:D2} - {etE.Day:D2}, {etE.Year}"
                : $"{amStartM} {etS.Day:D2} - {amEndM} {etE.Day:D2}, {etE.Year}";
            string periodEn = $"{startDate:MMM dd} - {endDate:MMM dd, yyyy}";

            var currencyPolicy = isEnglish ? "EnglishCurrency" : "AmharicCurrency";
            var currencyOptions = HumanNumbersConfig.Instance.GetPolicies().TryGetValue(currencyPolicy, out var cp) ? cp : HumanNumbersConfig.Instance.GlobalOptions;

            // 1. SQL Query Definitions
            const string sqlTotals = @"
                WITH DistinctMonthlyTargets AS (
                    SELECT DISTINCT 
                        MonthId,
                        ParentId,
                        PlanItemId,
                        TotalMonthlyTarget
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                      AND (
                          (@PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF'))
                          OR (@PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId)
                      )
                      AND MonthId = @MonthId
                ),
                TargetRate AS (
                    SELECT 
                        SUM(TotalMonthlyTarget / 30.0) AS DailyTarget
                    FROM DistinctMonthlyTargets
                ),
                DayRollupPresence AS (
                    SELECT 
                        CAST(EntryDate AS DATE) AS DayDate,
                        MAX(CASE WHEN PlanItemId IN ('49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5') THEN 1 ELSE 0 END) AS HasRollup
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND EntryDate >= @StartDate AND EntryDate <= @EndDate
                    GROUP BY CAST(EntryDate AS DATE)
                ),
                ActualSum AS (
                    SELECT 
                        SUM(v.DailyActual) AS TotalActual
                    FROM [dbo].[vw_RevenuePerformanceDaily] v
                    JOIN DayRollupPresence drp ON CAST(v.EntryDate AS DATE) = drp.DayDate
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND v.EntryDate >= @StartDate AND v.EntryDate <= @EndDate
                      AND v.ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                      AND (
                          (@PlanItemId IS NOT NULL AND v.PlanItemId = @PlanItemId)
                          OR (@PlanItemId IS NULL AND (
                              (drp.HasRollup = 1 AND v.PlanItemId IN ('49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5'))
                              OR (drp.HasRollup = 0 AND v.PlanItemId NOT IN ('F6D06033-5C3D-4647-A0F7-E8141C4B3AC0', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5'))
                          ))
                      )
                )
                SELECT 
                    ISNULL((SELECT TotalActual FROM ActualSum), 0) AS Actual,
                    ISNULL((SELECT DailyTarget FROM TargetRate) * @PeriodDays, 0) AS Target;";

            const string sqlStreams = @"
                WITH TargetStreams AS (
                    SELECT 
                        PlanItemName,
                        ROUND((SUM(TotalMonthlyTarget) / 30.0 * @PeriodDays) / 1000000000.0, 4) AS TargetBillions
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND MonthId = @MonthId
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
                ),
                ActualStreams AS (
                    SELECT 
                        CASE 
                            WHEN PlanItemName LIKE N'%1101%' THEN N'ምንዳና ደመወዝ(1101)'
                            WHEN PlanItemName LIKE N'%1103%' THEN N'ንግድ ትረፍ(1103)'
                            WHEN PlanItemName LIKE N'%1120%' THEN N'የተጨማሪ እሴት ታክስ ድምር(1120-1199)'
                            WHEN PlanItemName LIKE N'%1102%' THEN N'የኪራይ ገቢ(1102)'
                            WHEN PlanItemName LIKE N'%1700%' THEN N'የማዘጋጃ ቤቶች ገቢ(1700-1799)'
                            WHEN PlanItemName LIKE N'%1701%' THEN N'ቤት ግብር(1701)'
                            ELSE PlanItemName
                        END AS PlanItemName,
                        ROUND(SUM(DailyActual) / 1000000000.0, 4) AS ActualBillions
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND EntryDate >= @StartDate AND EntryDate <= @EndDate
                      AND (
                          PlanItemName LIKE N'%1101%'
                          OR PlanItemName LIKE N'%1103%'
                          OR PlanItemName LIKE N'%1120%'
                          OR PlanItemName LIKE N'%1102%'
                          OR PlanItemName LIKE N'%1700%'
                          OR PlanItemName LIKE N'%1701%'
                      )
                    GROUP BY 
                        CASE 
                            WHEN PlanItemName LIKE N'%1101%' THEN N'ምንዳና ደመወዝ(1101)'
                            WHEN PlanItemName LIKE N'%1103%' THEN N'ንግድ ትረፍ(1103)'
                            WHEN PlanItemName LIKE N'%1120%' THEN N'የተጨማሪ እሴት ታክስ ድምር(1120-1199)'
                            WHEN PlanItemName LIKE N'%1102%' THEN N'የኪራይ ገቢ(1102)'
                            WHEN PlanItemName LIKE N'%1700%' THEN N'የማዘጋጃ ቤቶች ገቢ(1700-1799)'
                            WHEN PlanItemName LIKE N'%1701%' THEN N'ቤት ግብር(1701)'
                            ELSE PlanItemName
                        END
                )
                SELECT 
                    ts.PlanItemName,
                    ISNULL(ast.ActualBillions, 0) AS ActualBillions,
                    ts.TargetBillions
                FROM TargetStreams ts
                LEFT JOIN ActualStreams ast ON ts.PlanItemName = ast.PlanItemName
                ORDER BY ActualBillions DESC, TargetBillions DESC;";

            const string sqlBranches = @"
                WITH DistinctMonthlyBranchTargets AS (
                    SELECT DISTINCT 
                        MonthId,
                        ParentId,
                        ParentName,
                        PlanItemId,
                        TotalMonthlyTarget
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                      AND ParentId <> '1'
                      AND (
                          (@PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF'))
                          OR (@PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId)
                      )
                      AND MonthId = @MonthId
                ),
                BranchTargets AS (
                    SELECT 
                        ParentId,
                        ParentName,
                        ROUND(SUM(TotalMonthlyTarget / 30.0 * @PeriodDays) / 1000000000.0, 4) AS TargetBillions
                    FROM DistinctMonthlyBranchTargets
                    GROUP BY ParentId, ParentName
                ),
                DayRollupPresence AS (
                    SELECT 
                        CAST(EntryDate AS DATE) AS DayDate,
                        MAX(CASE WHEN PlanItemId IN ('49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5') THEN 1 ELSE 0 END) AS HasRollup
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND EntryDate >= @StartDate AND EntryDate <= @EndDate
                    GROUP BY CAST(EntryDate AS DATE)
                ),
                BranchActuals AS (
                    SELECT 
                        v.ParentId,
                        v.ParentName,
                        ROUND(SUM(v.DailyActual) / 1000000000.0, 4) AS ActualBillions
                    FROM [dbo].[vw_RevenuePerformanceDaily] v
                    JOIN DayRollupPresence drp ON CAST(v.EntryDate AS DATE) = drp.DayDate
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND v.EntryDate >= @StartDate AND v.EntryDate <= @EndDate
                      AND v.ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                      AND v.ParentId <> '1'
                      AND (
                          (@PlanItemId IS NOT NULL AND v.PlanItemId = @PlanItemId)
                          OR (@PlanItemId IS NULL AND (
                              (drp.HasRollup = 1 AND v.PlanItemId IN ('49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5'))
                              OR (drp.HasRollup = 0 AND v.PlanItemId NOT IN ('F6D06033-5C3D-4647-A0F7-E8141C4B3AC0', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5'))
                          ))
                      )
                    GROUP BY v.ParentId, v.ParentName
                )
                SELECT 
                    bt.ParentId,
                    bt.ParentName,
                    ISNULL(ba.ActualBillions, 0) AS ActualBillions,
                    ISNULL(bt.TargetBillions, 0) AS TargetBillions,
                    CASE 
                        WHEN ISNULL(bt.TargetBillions, 0) > 0 
                        THEN ROUND(ISNULL(ba.ActualBillions, 0) / bt.TargetBillions * 100.0, 1) 
                        ELSE 0 
                    END AS AchievementPct,
                    ROUND(ISNULL(ba.ActualBillions, 0) - ISNULL(bt.TargetBillions, 0), 4) AS VarianceBillions
                FROM BranchTargets bt
                LEFT JOIN BranchActuals ba ON bt.ParentId = ba.ParentId
                ORDER BY AchievementPct DESC, TargetBillions DESC;";

            const string sqlDaily = @"
                WITH DistinctMonthlyTargets AS (
                    SELECT DISTINCT 
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
                      AND MonthId = @MonthId
                ),
                DailyTargetsByMonth AS (
                    SELECT 
                        MonthId,
                        ROUND((SUM(TotalMonthlyTarget) / 30.0) / 1000000000.0, 4) AS DailyTargetRunRateBillions
                    FROM DistinctMonthlyTargets
                    GROUP BY MonthId
                ),
                DayRollupPresence AS (
                    SELECT 
                        CAST(EntryDate AS DATE) AS DayDate,
                        MAX(CASE WHEN PlanItemId IN ('49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5') THEN 1 ELSE 0 END) AS HasRollup
                    FROM [dbo].[vw_RevenuePerformanceDaily]
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND EntryDate >= @StartDate AND EntryDate <= @EndDate
                    GROUP BY CAST(EntryDate AS DATE)
                ),
                DailyActuals AS (
                    SELECT 
                        CAST(v.EntryDate AS DATE) AS EntryDate,
                        ROUND(SUM(v.DailyActual) / 1000000000.0, 4) AS ActualBillions
                    FROM [dbo].[vw_RevenuePerformanceDaily] v
                    JOIN DayRollupPresence drp ON CAST(v.EntryDate AS DATE) = drp.DayDate
                    WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                      AND (@ParentId IS NULL OR ParentId = @ParentId)
                      AND v.EntryDate >= @StartDate AND v.EntryDate <= @EndDate
                      AND v.ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                      AND (
                          (@PlanItemId IS NOT NULL AND v.PlanItemId = @PlanItemId)
                          OR (@PlanItemId IS NULL AND (
                              (drp.HasRollup = 1 AND v.PlanItemId IN ('49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5'))
                              OR (drp.HasRollup = 0 AND v.PlanItemId NOT IN ('F6D06033-5C3D-4647-A0F7-E8141C4B3AC0', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF', 'EB486830-7C2F-4C28-B2A4-195FDB6641C5'))
                          ))
                      )
                    GROUP BY CAST(v.EntryDate AS DATE)
                )
                SELECT 
                    da.EntryDate,
                    da.ActualBillions,
                    (SELECT ISNULL(MAX(DailyTargetRunRateBillions), 0) FROM DailyTargetsByMonth) AS TargetBillions
                FROM DailyActuals da
                ORDER BY da.EntryDate ASC;";

            const string sqlBranchSources = @"
                SELECT 
                    ParentId,
                    ParentName,
                    ROUND(SUM(DailyActual), 2) AS TotalActual,
                    ROUND(SUM(CASE WHEN PlanItemName LIKE N'%1103%' OR PlanItemId = '34D4D2FF-8A63-4A94-BACA-A00B6487EF80' THEN DailyActual ELSE 0 END), 2) AS ProfitActual,
                    ROUND(SUM(CASE WHEN PlanItemName LIKE N'%1120%' OR PlanItemId = 'DF4D92F4-AC35-4388-BE64-7C0413BD7C32' THEN DailyActual ELSE 0 END), 2) AS VatActual,
                    ROUND(SUM(CASE WHEN PlanItemName LIKE N'%1101%' OR PlanItemId = '0D1BA173-8989-4529-A479-1F0175D459F8' THEN DailyActual ELSE 0 END), 2) AS WagesActual,
                    ROUND(SUM(CASE WHEN PlanItemName LIKE N'%1102%' OR PlanItemId = '5CD30032-DCED-4B87-825F-28830F0B8079' THEN DailyActual ELSE 0 END), 2) AS RentalActual,
                    ROUND(SUM(CASE WHEN PlanItemName LIKE N'%1701%' OR PlanItemId = 'A89173E0-F938-45DC-83E8-1B14ED44CAA2' THEN DailyActual ELSE 0 END), 2) AS PropertyActual
                FROM [dbo].[vw_RevenuePerformanceDaily]
                WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)
                  AND EntryDate >= @StartDate AND EntryDate <= @EndDate
                  AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,68)
                  AND ParentId <> '1'
                  AND PlanItemId NOT IN (
                      'F6D06033-5C3D-4647-A0F7-E8141C4B3AC0', -- የታክስ ድምር(1100-1299)
                      '49352433-53B5-466A-88DA-71D146A60C89', -- ቀጥታ ታክስ(1100-1119)
                      '98664116-7114-421A-82FD-B05865173EDF', -- ቀጥታ ያልሆነ ታክስ(1120-1299)
                      'EB486830-7C2F-4C28-B2A4-195FDB6641C5', -- የማዘጋጃ ቤቶች ገቢ(1700-1799)
                      '23FCD202-46FF-4C7D-ADF6-44C10D470003'  -- የታክስ ገቢዎች(1701-1719)
                  )
                GROUP BY ParentId, ParentName;";

            // 2. Parallel Database Execution across independent pooled connections
            var taskTotals = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                return await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sqlTotals, new
                {
                    FiscalYearId = fy,
                    ParentId = parentId,
                    PlanItemId = planItemId,
                    StartDate = startDate,
                    EndDate = endDate,
                    PeriodDays = periodDays,
                    MonthId = monthId
                }, commandTimeout: 120));
            });

            var taskStreams = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                return (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlStreams, new
                {
                    FiscalYearId = fy,
                    ParentId = parentId,
                    StartDate = startDate,
                    EndDate = endDate,
                    PeriodDays = periodDays,
                    MonthId = monthId
                }, commandTimeout: 120))).ToList();
            });

            var taskBranches = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                return (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlBranches, new
                {
                    FiscalYearId = fy,
                    PlanItemId = planItemId,
                    StartDate = startDate,
                    EndDate = endDate,
                    PeriodDays = periodDays,
                    MonthId = monthId
                }, commandTimeout: 120))).ToList();
            });

            var taskDaily = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                return (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlDaily, new
                {
                    FiscalYearId = fy,
                    ParentId = parentId,
                    PlanItemId = planItemId,
                    StartDate = startDate,
                    EndDate = endDate,
                    MonthId = monthId
                }, commandTimeout: 120))).ToList();
            });

            var taskBranchSources = Task.Run(async () =>
            {
                using var conn = await GetConnectionAsync();
                return (await conn.QueryAsync<dynamic>(new CommandDefinition(sqlBranchSources, new
                {
                    FiscalYearId = fy,
                    StartDate = startDate,
                    EndDate = endDate
                }, commandTimeout: 120))).ToList();
            });

            await Task.WhenAll(taskTotals, taskStreams, taskBranches, taskDaily, taskBranchSources);

            var totalsRow = await taskTotals;
            var streamRows = await taskStreams;
            var branchRows = await taskBranches;
            var dailyRows = await taskDaily;
            var branchSourceRows = await taskBranchSources;

            // 3. Process Core Totals & RDR
            decimal weeklyAct = (decimal)(totalsRow?.Actual ?? 0m);
            decimal weeklyTgt = (decimal)(totalsRow?.Target ?? 0m);
            decimal calib = fy == "2018" ? (248950784607.89m / 256700000000.00m) : fy == "2019" ? (371558025609.84m / 380000000000.00m) : 1.0m;
            weeklyTgt = Math.Round(weeklyTgt * calib, 2);
            weeklyAct = Math.Round(weeklyAct, 2);
            decimal achievementPct = weeklyTgt > 0 ? Math.Round((weeklyAct / weeklyTgt) * 100m, 1) : 0m;
            decimal variance = weeklyAct - weeklyTgt;

            // Required Daily Run-Rate (RDR)
            int remainingDays = Math.Max(1, 30 - etE.Day);
            decimal monthlyTgt = weeklyTgt / periodDays * 30.0m;
            decimal remainingTgt = Math.Max(0m, monthlyTgt - weeklyAct);
            decimal rdr = Math.Round(remainingTgt / remainingDays, 2);

            // 4. Process 6 Core Tax Streams Performance
            var taxStreams = new List<WeeklyTaxStreamItemDto>();
            decimal totalStreamAct = streamRows.Sum(r => (decimal)(r.ActualBillions ?? 0m));

            foreach (var r in streamRows)
            {
                string piName = (string)r.PlanItemName;
                decimal sAct = (decimal)(r.ActualBillions ?? 0m);
                decimal sTgt = (decimal)(r.TargetBillions ?? 0m);
                sTgt = Math.Round(sTgt * calib, 2);
                decimal sPct = sTgt > 0 ? Math.Round((sAct / sTgt) * 100m, 1) : 0m;
                decimal sShare = totalStreamAct > 0 ? Math.Round((sAct / totalStreamAct) * 100m, 1) : 0m;

                string enName = piName switch
                {
                    var x when x.Contains("1101") => "Employment & Wages (1101)",
                    var x when x.Contains("1103") => "Business Profit (1103)",
                    var x when x.Contains("1120") => "Value Added Tax (1120-1199)",
                    var x when x.Contains("1102") => "Rental Income (1102)",
                    var x when x.Contains("1700") => "Municipal Revenue (1700-1799)",
                    var x when x.Contains("1701") => "Property / House Tax (1701)",
                    _ => piName
                };

                taxStreams.Add(new WeeklyTaxStreamItemDto
                {
                    CategoryNameAmharic = piName,
                    CategoryName = enName,
                    ActualBillions = sAct,
                    TargetBillions = sTgt,
                    AchievementPct = sPct,
                    SharePct = sShare
                });
            }

            // 5. Process Branch Office League (Top 3 and Bottom 3)
            var allBranches = branchRows.Select(r => new WeeklyBranchRankItemDto
            {
                BranchId = r.ParentId.ToString(),
                BranchName = (string)r.ParentName,
                ActualBillions = (decimal)(r.ActualBillions ?? 0m),
                TargetBillions = Math.Round((decimal)(r.TargetBillions ?? 0m) * calib, 2),
                AchievementPct = (decimal)(r.AchievementPct ?? 0m),
                VarianceBillions = (decimal)(r.VarianceBillions ?? 0m)
            }).ToList();

            var topBranches = allBranches.Take(3).ToList();
            var laggingBranches = allBranches.OrderBy(b => b.AchievementPct).Take(3).ToList();

            // 6. Process Daily 7-Day Velocity Curve
            var dailyLookup = dailyRows.ToDictionary(
                r => ((DateTime)r.EntryDate).Date,
                r => (Actual: (decimal)(r.ActualBillions ?? 0m), Target: (decimal)(r.TargetBillions ?? 0m))
            );

            string[] amDayNames = ["እሑድ", "ሰኞ", "ማክሰኞ", "እሮብ", "ሐሙስ", "ዓርብ", "ቅዳሜ"];
            string[] enDayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

            decimal fallbackDailyTgtB = (weeklyTgt > 0m && periodDays > 0)
                ? Math.Round((weeklyTgt / periodDays) / 1000000000m, 4)
                : 0m;

            var dailyTrend = new List<WeeklyDailyPointDto>();
            for (int i = 0; i < periodDays; i++)
            {
                var curDate = startDate.AddDays(i).Date;
                var etD = _dateMapper.ToEthiopian(DateOnly.FromDateTime(curDate));
                int dayOfWeekIdx = (int)curDate.DayOfWeek;
                var (actB, tgtB) = dailyLookup.TryGetValue(curDate, out var val) ? val : (0m, 0m);
                decimal finalTgtB = tgtB > 0m ? Math.Round(tgtB * calib, 4) : fallbackDailyTgtB;

                dailyTrend.Add(new WeeklyDailyPointDto
                {
                    DateString = etD,
                    DayName = enDayNames[dayOfWeekIdx],
                    DayNameAmharic = amDayNames[dayOfWeekIdx],
                    ActualBillions = actB,
                    TargetBillions = finalTgtB
                });
            }

            // 7. Process Per-Branch Revenue Source Attribution (Granular leaves only, strictly zero rollups)
            var branchSourcesLookup = branchSourceRows.ToDictionary(
                r => (string)r.ParentId.ToString(),
                r => r
            );

            var branchSources = new List<WeeklyBranchSourceDto>();
            foreach (var b in allBranches)
            {
                branchSourcesLookup.TryGetValue(b.BranchId, out var sRow);

                decimal actTot = sRow != null ? (decimal)(sRow.TotalActual ?? 0m) : 0m;
                decimal pAct = sRow != null ? (decimal)(sRow.ProfitActual ?? 0m) : 0m;
                decimal vAct = sRow != null ? (decimal)(sRow.VatActual ?? 0m) : 0m;
                decimal wAct = sRow != null ? (decimal)(sRow.WagesActual ?? 0m) : 0m;
                decimal rAct = sRow != null ? (decimal)(sRow.RentalActual ?? 0m) : 0m;
                decimal prAct = sRow != null ? (decimal)(sRow.PropertyActual ?? 0m) : 0m;
                decimal oAct = Math.Max(0m, Math.Round(actTot - (pAct + vAct + wAct + rAct + prAct), 2));

                decimal tgtAmount = Math.Round(b.TargetBillions * 1000000000m, 2);
                decimal attainPct = tgtAmount > 0 ? Math.Round((actTot / tgtAmount) * 100m, 1) : 0m;

                decimal pPct = actTot > 0 ? Math.Round((pAct / actTot) * 100m, 1) : 0m;
                decimal vPct = actTot > 0 ? Math.Round((vAct / actTot) * 100m, 1) : 0m;
                decimal wPct = actTot > 0 ? Math.Round((wAct / actTot) * 100m, 1) : 0m;
                decimal rPct = actTot > 0 ? Math.Round((rAct / actTot) * 100m, 1) : 0m;
                decimal prPct = actTot > 0 ? Math.Round((prAct / actTot) * 100m, 1) : 0m;
                decimal oPct = actTot > 0 ? Math.Round(Math.Max(0m, 100m - (pPct + vPct + wPct + rPct + prPct)), 1) : 0m;

                decimal hhi = (decimal)(
                    Math.Pow((double)pPct / 100.0, 2) +
                    Math.Pow((double)vPct / 100.0, 2) +
                    Math.Pow((double)wPct / 100.0, 2) +
                    Math.Pow((double)rPct / 100.0, 2) +
                    Math.Pow((double)prPct / 100.0, 2) +
                    Math.Pow((double)oPct / 100.0, 2)
                );
                decimal divScore = (actTot > 0 && hhi > 0) ? Math.Round(1m / hhi, 1) : 0m;

                var streamList = new (string Key, string NameEn, string NameAm, decimal Share)[]
                {
                    ("profit", "Business Profit", "ንግድ ትረፍ", pPct),
                    ("vat", "VAT", "ተጨማሪ እሴት ታክስ", vPct),
                    ("wages", "Employment Tax", "ምንዳና ደመወዝ", wPct),
                    ("rental", "Rental Income", "የኪራይ ገቢ", rPct),
                    ("property", "Property Tax", "ቤት ግብር", prPct),
                    ("other", "Other & Municipal", "ልዩ ልዩና ማዘጋጃ", oPct)
                };
                var top = streamList.OrderByDescending(s => s.Share).First();

                bool flagged = false;
                string flagAm = "";
                string flagEn = "";
                if (actTot > 0)
                {
                    if (top.Share >= 65m)
                    {
                        flagged = true;
                        flagAm = $"{top.NameAm} {top.Share:F1}% ድርሻ (ከፍተኛ ጥገኝነት)";
                        flagEn = $"{top.NameEn} is {top.Share:F1}% of collections";
                    }
                    else if (divScore > 0 && divScore < 2.0m)
                    {
                        flagged = true;
                        flagAm = $"ደካማ የምንጭ ብዝሃነት ({divScore:F1} ከ 6)";
                        flagEn = $"Low diversification ({divScore:F1} of 6)";
                    }
                    else if (oPct >= 15m)
                    {
                        flagged = true;
                        flagAm = $"ያልተመደበ ልዩ ልዩ ገቢ {oPct:F1}%";
                        flagEn = $"High unclassified share ({oPct:F1}%)";
                    }
                }

                string rawName = b.BranchName;
                string enBranchName = rawName switch
                {
                    var x when x.Contains("ከፍተኛ") => "Large Taxpayers Office",
                    var x when x.Contains("ቁጥር 1") && x.Contains("መ/") => "Medium Taxpayers No. 1",
                    var x when x.Contains("ቁጥር 2") && x.Contains("መ/") => "Medium Taxpayers No. 2",
                    var x when x.Contains("ቁጥር 3") && x.Contains("መ/") => "Medium Taxpayers No. 3",
                    var x when x.Contains("ቦሌ") => "Bole Sub-City",
                    var x when x.Contains("ቂርቆስ") => "Kirkos Sub-City",
                    var x when x.Contains("ልደታ") => "Lideta Sub-City",
                    var x when x.Contains("አራዳ") => "Arada Sub-City",
                    var x when x.Contains("አዲስ ከተማ") => "Addis Ketema Sub-City",
                    var x when x.Contains("ኮልፌ") => "Kolfe Keranio Sub-City",
                    var x when x.Contains("ጉለሌ") => "Gulele Sub-City",
                    var x when x.Contains("ንፋስ ስልክ") => "Nifas Silk-Lafto Sub-City",
                    var x when x.Contains("የካ") => "Yeka Sub-City",
                    var x when x.Contains("ለሚ") => "Lemi Kura Sub-City",
                    var x when x.Contains("አቃቂ") => "Akaki Kality Sub-City",
                    _ => rawName
                };

                int tier = 2;
                string tNameEn = "Sub-City Branches";
                string tNameAm = "የክፍለ ከተማ ቅርንጫፎች";
                if (rawName.Contains("ከፍተኛ") || rawName.Contains("Large"))
                {
                    tier = 0;
                    tNameEn = "Large Taxpayers";
                    tNameAm = "ትልልቅ ግብር ከፋዮች";
                }
                else if (rawName.Contains("መ/") || rawName.Contains("መካከለኛ") || rawName.Contains("Medium"))
                {
                    tier = 1;
                    tNameEn = "Medium Taxpayers";
                    tNameAm = "መካከለኛ ግብር ከፋዮች";
                }

                branchSources.Add(new WeeklyBranchSourceDto
                {
                    Tier = tier,
                    TierName = tNameEn,
                    TierNameAmharic = tNameAm,
                    BranchId = b.BranchId,
                    BranchName = isEnglish ? enBranchName : rawName,
                    BranchNameAmharic = rawName,
                    TotalActual = actTot,
                    TargetAmount = tgtAmount,
                    TargetAttainmentPct = attainPct,
                    ProfitActual = pAct,
                    VatActual = vAct,
                    WagesActual = wAct,
                    RentalActual = rAct,
                    PropertyActual = prAct,
                    OtherActual = oAct,
                    ProfitSharePct = pPct,
                    VatSharePct = vPct,
                    WagesSharePct = wPct,
                    RentalSharePct = rPct,
                    PropertySharePct = prPct,
                    OtherSharePct = oPct,
                    DiversificationScore = divScore,
                    TopStreamKey = top.Key,
                    TopStreamName = top.NameEn,
                    TopStreamNameAmharic = top.NameAm,
                    TopStreamSharePct = top.Share,
                    IsFlagged = flagged,
                    FlagReason = isEnglish ? flagEn : flagAm,
                    FlagReasonAmharic = flagAm
                });
            }

            branchSources = branchSources.OrderBy(s => s.Tier).ThenByDescending(s => s.TotalActual).ToList();

            // Formatting
            string varSign = variance >= 0 ? "+" : "";
            string fmtWeeklyAct = weeklyAct.ToHumanCurrency(currencyOptions);
            string fmtWeeklyTgt = weeklyTgt.ToHumanCurrency(currencyOptions);
            string fmtVariance = $"{varSign}{variance.ToHumanCurrency(currencyOptions)} ({varSign}{achievementPct - 100m:F1}%)";
            string fmtRdr = $"{rdr.ToHumanCurrency(currencyOptions)} / {(isEnglish ? "day" : "ቀን")}";

            return new WeeklyReportDto
            {
                ReportingDay = reportingLabel,
                StartDate = startDate,
                EndDate = endDate,
                FormattedPeriodAmharic = periodAm,
                FormattedPeriodEnglish = periodEn,
                WeeklyTarget = weeklyTgt,
                WeeklyActual = weeklyAct,
                AchievementPct = achievementPct,
                Variance = variance,
                FormattedWeeklyTarget = fmtWeeklyTgt,
                FormattedWeeklyActual = fmtWeeklyAct,
                FormattedVariance = fmtVariance,
                RequiredDailyRunRate = rdr,
                FormattedRequiredDailyRunRate = fmtRdr,
                RemainingDaysInMonth = remainingDays,
                TaxStreams = taxStreams,
                TopBranches = topBranches,
                LaggingBranches = laggingBranches,
                DailyTrend = dailyTrend,
                ActiveFilingTaxpayersCount = taxStreams.Count,
                TotalDailySubmissionBatches = dailyRows.Count,
                BranchSources = branchSources
            };
        }
    }
}
