namespace RevenueOperationsDashboard.Models  
{  
    public class DashboardFilterDto  
    {  
        public string? FiscalYearId { get; set; }  
        public string? ParentId { get; set; }  
        public string? PlanItemId { get; set; }  
    }

    public class TaxTypePerformanceDto  
    {  
        public List<string> Categories { get; set; } = new();  
        public List<decimal> TargetBillions { get; set; } = new();  
        public List<decimal> ActualBillions { get; set; } = new();  
    }

    public class MonthlyTrendDto  
    {  
        public List<string> Months { get; set; } = new();  
        public List<decimal> Achievements { get; set; } = new();  
    }

    public class BranchRankingDto  
    {  
        public List<string> Branches { get; set; } = new();  
        public List<decimal> Scores { get; set; } = new();  
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

    public class DashboardConfigDto
    {
        public string DefaultLanguage { get; set; } = "en";
        public DefaultFiltersDto DefaultFilters { get; set; } = new();
        public CarouselConfigDto Carousel { get; set; } = new();
    }

    public class DefaultFiltersDto
    {
        public string FiscalYearId { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public string PlanItemId { get; set; } = string.Empty;
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
        public bool Visible { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleAm { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string SubtitleAm { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = string.Empty;
        public string ComparisonMode { get; set; } = string.Empty; // YoY, YTD, None
        public string ChartType { get; set; } = string.Empty; // bar-grouped, line-smooth, leaderboard-split, etc.
        public int? DurationMs { get; set; }
    }

    public class TopKpiCardItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string TitleAmharic { get; set; } = string.Empty;
        public string TitleEnglish { get; set; } = string.Empty;
        public decimal Actual { get; set; }
        public decimal Target { get; set; }
        public decimal AchievementPct { get; set; }
        public string Unit { get; set; } = "ETB";
        public string FormattedActual { get; set; } = string.Empty;
        public string FormattedTarget { get; set; } = string.Empty;
    }

    public class TopCardsResponseDto
    {
        public List<TopKpiCardItemDto> Cards { get; set; } = new();
    }
}
