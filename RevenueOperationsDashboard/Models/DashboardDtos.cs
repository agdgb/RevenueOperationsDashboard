namespace RevenueOperationsDashboard.Models  
{  
    public class DashboardFilterDto  
    {  
        public string? FiscalYearId { get; set; }  
        public string? ParentId { get; set; }  
        public string? PlanItemId { get; set; }  
        public string? Lang { get; set; }  
        public string? StartDate { get; set; } // Ethiopian date: yyyy-MM-dd
        public string? EndDate { get; set; }   // Ethiopian date: yyyy-MM-dd
        public string? DatePreset { get; set; } // ALL, Q1, Q2, Q3, Q4, TODAY, THIS_WEEK, THIS_MONTH, CUSTOM
        public string? CalculationMode { get; set; } // MonthlyRunRate, CumulativeYtd
    }

    public class DailyTrendDto
    {
        public List<string> Dates { get; set; } = new();
        public List<decimal> ActualBillions { get; set; } = new();
        public List<decimal> TargetBillions { get; set; } = new();
    }

    public class TaxTypePerformanceDto  
    {  
        public List<string> Categories { get; set; } = new();  
        public List<decimal> TargetBillions { get; set; } = new();  
        public List<decimal> ActualBillions { get; set; } = new();  
        public List<decimal> TargetAmounts { get; set; } = new();  
        public List<decimal> ActualAmounts { get; set; } = new();  
        public List<decimal> AchievementPcts { get; set; } = new();  
        public List<string> FormattedTargets { get; set; } = new();  
        public List<string> FormattedActuals { get; set; } = new();  
    }

    public class MonthlyTrendDto  
    {  
        public List<string> Months { get; set; } = new();  
        public List<decimal> Achievements { get; set; } = new();  
        public List<decimal> ActualAmounts { get; set; } = new();  
        public List<decimal> TargetAmounts { get; set; } = new();  
        public List<string> FormattedActuals { get; set; } = new();  
        public List<string> FormattedTargets { get; set; } = new();  
    }

    public class YoYMonthlyTrendDto
    {
        public List<string> Months { get; set; } = new();
        public List<decimal> CurrentYearAchievements { get; set; } = new();
        public List<decimal> PreviousYearAchievements { get; set; } = new();
        public List<decimal> CurrentYearActualAmounts { get; set; } = new();
        public List<decimal> PreviousYearActualAmounts { get; set; } = new();
        public List<string> CurrentYearFormattedActuals { get; set; } = new();
        public List<string> PreviousYearFormattedActuals { get; set; } = new();
        public string CurrentYearLabel { get; set; } = string.Empty;
        public string PreviousYearLabel { get; set; } = string.Empty;
    }

    public class BranchRankingDto  
    {  
        public List<string> Branches { get; set; } = new();  
        public List<decimal> Scores { get; set; } = new();  
        public List<decimal> ActualAmounts { get; set; } = new();  
        public List<decimal> TargetAmounts { get; set; } = new();  
        public List<string> FormattedActuals { get; set; } = new();  
        public List<string> FormattedTargets { get; set; } = new();  
    }

    public class BranchVolumesDto  
    {  
        public List<string> Branches { get; set; } = new();  
        public List<decimal> TargetBillions { get; set; } = new();  
        public List<decimal> ActualBillions { get; set; } = new();  
    }

    public class FilterOptionsDto  
    {  
        public List<FilterItem> FiscalYears { get; set; } = new();  
        public List<FilterItem> Branches { get; set; } = new();  
        public List<FilterItem> TaxTypes { get; set; } = new();  
    }

    public class FilterItem  
    {  
        public string Id { get; set; } = string.Empty;  
        public string Name { get; set; } = string.Empty;  
    }  

    public class WeeklyReportSettingsDto
    {
        public bool Enabled { get; set; } = true;
        public int PeriodDays { get; set; } = 7;
        public string TitleAm { get; set; } = "ሳምንታዊ የገቢ አፈፃፀም ሪፖርት";
        public string TitleEn { get; set; } = "Executive Weekly Performance Report";
        public string SubtitleAm { get; set; } = "የ 7 ቀናት ተከታታይ የስራ አፈፃፀም፣ የታክስ አይነቶች እና የቅርንጫፎች ደረጃ";
        public string SubtitleEn { get; set; } = "Past 7 days performance, revenue streams, and branch rankings";
    }

    public class DashboardConfigDto
    {
        public string DefaultLanguage { get; set; } = "am";
        public string KpiCalculationMode { get; set; } = "ToDateTarget";
        public string DefaultChartMetricMode { get; set; } = "wicks"; // "wicks", "pct", "amount"
        public string DefaultTheme { get; set; } = "ocean";
        public DefaultFiltersDto DefaultFilters { get; set; } = new();
        public WeeklyReportSettingsDto WeeklyReport { get; set; } = new();
        public CarouselConfigDto Carousel { get; set; } = new();
    }

    public class DefaultFiltersDto
    {
        public string FiscalYearId { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public string PlanItemId { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string DatePreset { get; set; } = "ALL";
        public string CalculationMode { get; set; } = "ToDateTarget";
    }

    public class CarouselConfigDto
    {
        public bool AutoPlay { get; set; }
        public int IntervalMs { get; set; }
        public List<SlideConfigDto> Slides { get; set; } = new();
    }

    public class SlideConfigDto
    {
        public string Id { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public string Title { get; set; } = string.Empty;
        public string TitleAm { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string SubtitleAm { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = string.Empty;
        public int? DurationMs { get; set; }
        public string? DefaultMetricMode { get; set; }
    }

    public class TopKpiCardItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string TitleAmharic { get; set; } = string.Empty;
        public string TitleEnglish { get; set; } = string.Empty;
        public string SubtitleAmharic { get; set; } = string.Empty;
        public string SubtitleEnglish { get; set; } = string.Empty;
        public string CalculationMode { get; set; } = "ToDateTarget";
        public decimal Actual { get; set; }
        public decimal Target { get; set; }
        public decimal AchievementPct { get; set; }
        public string Unit { get; set; } = "ETB";
        public string FormattedActual { get; set; } = string.Empty;
        public string FormattedTarget { get; set; } = string.Empty;
        public bool IsOverridden { get; set; } = false;
        public bool UseHumanFormat { get; set; } = true;
    }

    public class TopCardsResponseDto
    {
        public string ActiveCalculationMode { get; set; } = "ToDateTarget";
        public string TopCardsMode { get; set; } = "Hybrid"; // "Auto", "Hybrid", "Manual"
        public bool HasActiveOverrides { get; set; } = false;
        public List<TopKpiCardItemDto> Cards { get; set; } = new();
    }

    public class TopCardOverrideDto
    {
        public string CardId { get; set; } = string.Empty;
        public string FiscalYearId { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = false;
        public decimal? Actual { get; set; }
        public decimal? Target { get; set; }
        public bool UseHumanFormat { get; set; } = true;
        public bool ApplyOnlyAllBranches { get; set; } = true;
        public string? Notes { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
    }

    public class TopCardOverridesSaveRequestDto
    {
        public string FiscalYearId { get; set; } = string.Empty;
        public string? TopCardsMode { get; set; } // Optional: to also update master mode
        public List<TopCardOverrideDto> Overrides { get; set; } = new();
    }

    public class TopCardSettingsStateDto
    {
        public string TopCardsMode { get; set; } = "Hybrid"; // "Auto", "Hybrid", "Manual"
        public string FiscalYearId { get; set; } = string.Empty;
        public List<TopCardOverrideDto> Overrides { get; set; } = new();
    }

    public class SystemSettingUpdateDto
    {
        public string Mode { get; set; } = "Hybrid";
    }

    public class WeeklyReportDto
    {
        public string ReportingDay { get; set; } = "Wednesday";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string FormattedPeriodAmharic { get; set; } = string.Empty;
        public string FormattedPeriodEnglish { get; set; } = string.Empty;

        // Core Revenue Totals
        public decimal WeeklyTarget { get; set; }
        public decimal WeeklyActual { get; set; }
        public decimal AchievementPct { get; set; }
        public decimal Variance { get; set; }
        public string FormattedWeeklyTarget { get; set; } = string.Empty;
        public string FormattedWeeklyActual { get; set; } = string.Empty;
        public string FormattedVariance { get; set; } = string.Empty;

        // Run-Rate Trajectory
        public decimal RequiredDailyRunRate { get; set; }
        public string FormattedRequiredDailyRunRate { get; set; } = string.Empty;
        public int RemainingDaysInMonth { get; set; }

        // Core Tax Streams Performance (6 Categories)
        public List<WeeklyTaxStreamItemDto> TaxStreams { get; set; } = new();

        // Branch Office League (Top 3 Overperforming & Bottom 3 Lagging)
        public List<WeeklyBranchRankItemDto> TopBranches { get; set; } = new();
        public List<WeeklyBranchRankItemDto> LaggingBranches { get; set; } = new();

        // Daily 7-Day Velocity Curve
        public List<WeeklyDailyPointDto> DailyTrend { get; set; } = new();

        // Demographic Activity Summary
        public int ActiveFilingTaxpayersCount { get; set; }
        public int TotalDailySubmissionBatches { get; set; }

        // Per-Branch Revenue Source Attribution (Leaves only, no rollups)
        public List<WeeklyBranchSourceDto> BranchSources { get; set; } = new();
    }

    public class WeeklyBranchSourceDto
    {
        public int Tier { get; set; } // 0 = Large, 1 = Medium, 2 = Sub-City
        public string TierName { get; set; } = string.Empty;
        public string TierNameAmharic { get; set; } = string.Empty;
        public string BranchId { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string BranchNameAmharic { get; set; } = string.Empty;
        public decimal TotalActual { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal TargetAttainmentPct { get; set; }
        public decimal ProfitActual { get; set; }
        public decimal VatActual { get; set; }
        public decimal WagesActual { get; set; }
        public decimal RentalActual { get; set; }
        public decimal PropertyActual { get; set; }
        public decimal OtherActual { get; set; }
        public decimal ProfitSharePct { get; set; }
        public decimal VatSharePct { get; set; }
        public decimal WagesSharePct { get; set; }
        public decimal RentalSharePct { get; set; }
        public decimal PropertySharePct { get; set; }
        public decimal OtherSharePct { get; set; }
        public decimal DiversificationScore { get; set; }
        public string TopStreamKey { get; set; } = string.Empty;
        public string TopStreamName { get; set; } = string.Empty;
        public string TopStreamNameAmharic { get; set; } = string.Empty;
        public decimal TopStreamSharePct { get; set; }
        public bool IsFlagged { get; set; }
        public string FlagReason { get; set; } = string.Empty;
        public string FlagReasonAmharic { get; set; } = string.Empty;
    }

    public class WeeklyTaxStreamItemDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryNameAmharic { get; set; } = string.Empty;
        public decimal TargetBillions { get; set; }
        public decimal ActualBillions { get; set; }
        public decimal AchievementPct { get; set; }
        public decimal SharePct { get; set; }
    }

    public class WeeklyBranchRankItemDto
    {
        public string BranchId { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public decimal TargetBillions { get; set; }
        public decimal ActualBillions { get; set; }
        public decimal AchievementPct { get; set; }
        public decimal VarianceBillions { get; set; }
    }

    public class WeeklyDailyPointDto
    {
        public string DateString { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public string DayNameAmharic { get; set; } = string.Empty;
        public decimal ActualBillions { get; set; }
        public decimal TargetBillions { get; set; }
    }
}
