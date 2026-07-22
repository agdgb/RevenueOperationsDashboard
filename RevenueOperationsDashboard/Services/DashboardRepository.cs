using Dapper;  
using Microsoft.Data.SqlClient;  
using RevenueOperationsDashboard.Models;

namespace RevenueOperationsDashboard.Services  
{  
    public class DashboardRepository  
    {  
        private readonly string _connectionString;

        public DashboardRepository(IConfiguration configuration)  
        {  
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;  
        }

        private SqlConnection GetConnection() => new SqlConnection(_connectionString);

        private string? NormalizeFilter(string? val) => 
            (string.IsNullOrEmpty(val) || val.Equals("ALL", StringComparison.OrdinalIgnoreCase)) ? null : val;

        // 1. Tax Type Performance (Billions ETB)  
        public async Task<TaxTypePerformanceDto> GetTaxTypePerformanceAsync(DashboardFilterDto filters)  
        {  
            const string sql = @"  
                SELECT   
                    PlanItemName,  
                    ROUND(SUM(TotalTarget) / 1000000000.0, 2) AS TargetBillions,  
                    ROUND(SUM(TotalActual) / 1000000000.0, 2) AS ActualBillions  
                FROM [dbo].[vw_RevenuePerformance_Clean]  
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

            using var conn = GetConnection();  
            var rows = (await conn.QueryAsync<dynamic>(sql, new {   
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),   
                ParentId = NormalizeFilter(filters.ParentId),   
                PlanItemId = NormalizeFilter(filters.PlanItemId)   
            })).ToList();

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
            const string sql = @"  
                SELECT   
                    MonthName,  
                    MonthOrder,  
                    CASE   
                        WHEN SUM(TotalTarget) > 0 THEN ROUND((SUM(TotalActual) / SUM(TotalTarget)) * 100, 2)  
                        ELSE 0   
                    END AS AchievementPct  
                FROM [dbo].[vw_RevenuePerformance_Clean]  
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
                GROUP BY MonthName, MonthOrder  
                ORDER BY MonthOrder ASC;";

            using var conn = GetConnection();  
            var rows = (await conn.QueryAsync<dynamic>(sql, new {   
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),   
                ParentId = NormalizeFilter(filters.ParentId),   
                PlanItemId = NormalizeFilter(filters.PlanItemId)   
            })).ToList();

            return new MonthlyTrendDto  
            {  
                Months = rows.Select(r => (string)r.MonthName).ToList(),  
                Achievements = rows.Select(r => (decimal)(r.AchievementPct ?? 0m)).ToList()  
            };  
        }

        // 3. Branch Performance Ranking (%)  
        public async Task<BranchRankingDto> GetBranchRankingAsync(DashboardFilterDto filters)  
        {  
            const string sql = @"  
                SELECT   
                    ParentName AS BranchName,  
                    CASE   
                        WHEN SUM(TotalTarget) > 0 THEN ROUND((SUM(TotalActual) / SUM(TotalTarget)) * 100, 2)  
                        ELSE 0   
                    END AS AchievementPct  
                FROM [dbo].[vw_RevenuePerformance_Clean]  
                WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
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
                GROUP BY ParentId, ParentName  
                ORDER BY AchievementPct ASC;";

            using var conn = GetConnection();  
            var rows = (await conn.QueryAsync<dynamic>(sql, new {   
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),   
                PlanItemId = NormalizeFilter(filters.PlanItemId)   
            })).ToList();

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

            using var conn = GetConnection();  
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
            const string sql = @"  
                SELECT   
                    ParentName AS BranchName,  
                    ROUND(SUM(TotalTarget) / 1000000000.0, 2) AS TargetBillions,  
                    ROUND(SUM(TotalActual) / 1000000000.0, 2) AS ActualBillions  
                FROM [dbo].[vw_RevenuePerformance_Clean]  
                WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
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
                GROUP BY ParentId, ParentName  
                ORDER BY TargetBillions ASC;";

            using var conn = GetConnection();  
            var rows = (await conn.QueryAsync<dynamic>(sql, new {   
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),   
                PlanItemId = NormalizeFilter(filters.PlanItemId)   
            })).ToList();

            var result = new BranchVolumesDto();
            foreach (var r in rows)
            {
                result.Branches.Add((string)r.BranchName ?? "Unknown");
                result.TargetBillions.Add((decimal)(r.TargetBillions ?? 0m));
                result.ActualBillions.Add((decimal)(r.ActualBillions ?? 0m));
            }
            return result;
        }
    }  
}
