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

        private SqlConnection GetConnection()
        {
            var conn = new SqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

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
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),
                ParentId = NormalizeFilter(filters.ParentId),
                PlanItemId = NormalizeFilter(filters.PlanItemId)
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
            const string sql = @"  
                SELECT   
                    MonthName,  
                    MonthOrder,  
                    CASE   
                        WHEN SUM(CASE 
                            WHEN @PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF') THEN TotalTarget
                            WHEN @PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId THEN TotalTarget
                            ELSE 0 
                        END) > 0 
                        THEN ROUND((SUM(CASE 
                            WHEN @PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF') THEN TotalActual
                            WHEN @PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId THEN TotalActual
                            ELSE 0 
                        END) / SUM(CASE 
                            WHEN @PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF') THEN TotalTarget
                            WHEN @PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId THEN TotalTarget
                            ELSE 0 
                        END)) * 100, 2)
                        ELSE 0 
                    END AS AchievementPct  
                FROM [dbo].[vw_RevenuePerformance_Detail]  
                WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                  AND (@ParentId IS NULL OR ParentId = @ParentId)  
                  AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16)
                GROUP BY MonthName, MonthOrder  
                ORDER BY MonthOrder ASC;";

            using var conn = GetConnection();
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),
                ParentId = NormalizeFilter(filters.ParentId),
                PlanItemId = NormalizeFilter(filters.PlanItemId)
            }, commandTimeout: 120))).ToList();


            return new MonthlyTrendDto  
            {  
                Months = rows.Select(r => (string)r.MonthName).ToList(),  
                Achievements = rows.Select(r => (decimal)(r.AchievementPct ?? 0m)).ToList()  
            };  
        }

        // 3. Branch Performance Ranking (%)  
        // Uses vw_RevenuePerformance and aggregates TotalSelectedActual/TotalSelectedTarget
        // across ALL fiscal years <= the selected year, matching the original dashboard.
        public async Task<BranchRankingDto> GetBranchRankingAsync(DashboardFilterDto filters)
        {
            const string sql = @"
                SELECT
                    ParentName AS BranchName,
                    CASE
                        WHEN SUM(TotalSelectedTarget) > 0
                        THEN ROUND(SUM(TotalSelectedActual) / SUM(TotalSelectedTarget) * 100, 2)
                        ELSE 0
                    END AS AchievementPct
                FROM [dbo].[vw_RevenuePerformance]
                WHERE (@FiscalYearId IS NULL OR FiscalYearId <= @FiscalYearId)
                  AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16)
                  AND MonthId IN (1,2,3,4,5,6,7,8,9,10,11,12)
                GROUP BY ParentId, ParentName  
                ORDER BY AchievementPct ASC;";

            using var conn = GetConnection();
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId)
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
                    ROUND(SUM(CASE 
                        WHEN @PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF') THEN TotalTarget
                        WHEN @PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId THEN TotalTarget
                        ELSE 0 
                    END) / 1000000000.0, 2) AS TargetBillions,  
                    ROUND(SUM(CASE 
                        WHEN @PlanItemId IS NULL AND PlanItemId IN ('EB486830-7C2F-4C28-B2A4-195FDB6641C5', '49352433-53B5-466A-88DA-71D146A60C89', '98664116-7114-421A-82FD-B05865173EDF') THEN TotalActual
                        WHEN @PlanItemId IS NOT NULL AND PlanItemId = @PlanItemId THEN TotalActual
                        ELSE 0 
                    END) / 1000000000.0, 2) AS ActualBillions  
                FROM [dbo].[vw_RevenuePerformance_Detail]  
                WHERE (@FiscalYearId IS NULL OR FiscalYearId = @FiscalYearId)  
                  AND ParentId IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16)
                GROUP BY ParentId, ParentName  
                ORDER BY TargetBillions ASC;";

            using var conn = GetConnection();
            var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new {
                FiscalYearId = NormalizeFilter(filters.FiscalYearId),
                PlanItemId = NormalizeFilter(filters.PlanItemId)
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
    }  
}
