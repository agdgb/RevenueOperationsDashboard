# **AI Agent Execution Plan: Operations Center Dashboard Migration**

## **1\. Overview & Objective**

You are tasked with building a lightweight, isolated dashboard application that delivers a high-performance TV display for an Operations Center.

The application will replace an embedded Bold BI dashboard with native **Apache ECharts** running on an **ASP.NET Core Web API** back-end powered by **Dapper**.

### **Key System Attributes:**

* **Database View:** \[dbo\].\[vw\_RevenuePerformance\_Detail\] in dashboard\_alpha\_db.  
* **Database Access:** Dapper micro-ORM (Raw parameterized SQL for maximum speed over view window functions).  
* **Frontend:** Single-page TV Operations Center display with dark theme, auto-rotating slides, floating side navigation arrows, and a global filter modal.

## **2\. Phase 1: Project Setup & Scaffolding**

1. Create a new standalone ASP.NET Core Web API project isolated from the core application repository:  
   dotnet new webapi \-n RevenueOperationsDashboard \-o RevenueOperationsDashboard  
   cd RevenueOperationsDashboard  
   dotnet add package Dapper  
   dotnet add package Microsoft.Data.SqlClient

2. Configure connection string in appsettings.json:  
   {  
     "ConnectionStrings": {  
       "DefaultConnection": "Server=YOUR\_SQL\_SERVER;Database=dashboard\_alpha\_db;Trusted\_Connection=True;TrustServerCertificate=True;"  
     }  
   }

3. Enable Static File Serving and CORS in Program.cs:  
   var builder \= WebApplication.CreateBuilder(args);

   builder.Services.AddControllers();  
   builder.Services.AddEndpointsApiExplorer();  
   builder.Services.AddSwaggerGen();

   builder.Services.AddCors(options \=\>  
   {  
       options.AddPolicy("AllowAll", policy \=\>  
           policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());  
   });

   builder.Services.AddScoped\<RevenueOperationsDashboard.Services.DashboardRepository\>();

   var app \= builder.Build();

   if (app.Environment.IsDevelopment())  
   {  
       app.UseSwagger();  
       app.UseSwaggerUI();  
   }

   app.UseStaticFiles(); // Serves HTML from wwwroot  
   app.UseCors("AllowAll");  
   app.UseAuthorization();  
   app.MapControllers();

   app.Run();

## **3\. Phase 2: Data Transfer Objects (DTOs)**

Create Models/DashboardDtos.cs to hold filter parameters and API response contracts:

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
        public List\<string\> Categories { get; set; } \= new();  
        public List\<decimal\> TargetBillions { get; set; } \= new();  
        public List\<decimal\> ActualBillions { get; set; } \= new();  
    }

    public class MonthlyTrendDto  
    {  
        public List\<string\> Months { get; set; } \= new();  
        public List\<decimal\> Achievements { get; set; } \= new();  
    }

    public class BranchRankingDto  
    {  
        public List\<string\> Branches { get; set; } \= new();  
        public List\<decimal\> Scores { get; set; } \= new();  
    }

    public class FilterOptionsDto  
    {  
        public List\<FilterItem\> FiscalYears { get; set; } \= new();  
        public List\<FilterItem\> Branches { get; set; } \= new();  
        public List\<FilterItem\> TaxTypes { get; set; } \= new();  
    }

    public class FilterItem  
    {  
        public string Id { get; set; } \= string.Empty;  
        public string Name { get; set; } \= string.Empty;  
    }  
}

## **4\. Phase 3: Dapper Repository Service**

Create Services/DashboardRepository.cs and implement fast, parameterized SQL queries against vw\_RevenuePerformance\_Detail:

using Dapper;  
using Microsoft.Data.SqlClient;  
using RevenueOperationsDashboard.Models;

namespace RevenueOperationsDashboard.Services  
{  
    public class DashboardRepository  
    {  
        private readonly string \_connectionString;

        public DashboardRepository(IConfiguration configuration)  
        {  
            \_connectionString \= configuration.GetConnectionString("DefaultConnection")\!;  
        }

        private SqlConnection GetConnection() \=\> new SqlConnection(\_connectionString);

        // 1\. Tax Type Performance (Billions ETB)  
        public async Task\<TaxTypePerformanceDto\> GetTaxTypePerformanceAsync(DashboardFilterDto filters)  
        {  
            const string sql \= @"  
                SELECT   
                    PlanItemName,  
                    ROUND(SUM(TotalTarget) / 1000000000.0, 2\) AS TargetBillions,  
                    ROUND(SUM(TotalActual) / 1000000000.0, 2\) AS ActualBillions  
                FROM \[dbo\].\[vw\_RevenuePerformance\_Detail\]  
                WHERE (@FiscalYearId IS NULL OR FiscalYearId \= @FiscalYearId)  
                  AND (@ParentId IS NULL OR ParentId \= @ParentId)  
                  AND (@PlanItemId IS NULL OR PlanItemId \= @PlanItemId)  
                GROUP BY PlanItemName  
                ORDER BY TargetBillions DESC;";

            using var conn \= GetConnection();  
            var rows \= (await conn.QueryAsync\<dynamic\>(sql, new {   
                filters.FiscalYearId,   
                filters.ParentId,   
                filters.PlanItemId   
            })).ToList();

            var result \= new TaxTypePerformanceDto();  
            foreach (var r in rows)  
            {  
                result.Categories.Add((string)r.PlanItemName);  
                result.TargetBillions.Add((decimal)(r.TargetBillions ?? 0m));  
                result.ActualBillions.Add((decimal)(r.ActualBillions ?? 0m));  
            }  
            return result;  
        }

        // 2\. Monthly Achievement Trend (%)  
        public async Task\<MonthlyTrendDto\> GetMonthlyTrendAsync(DashboardFilterDto filters)  
        {  
            const string sql \= @"  
                SELECT   
                    MonthName,  
                    MonthOrder,  
                    CASE   
                        WHEN SUM(TotalTarget) \> 0 THEN ROUND((SUM(TotalActual) / SUM(TotalTarget)) \* 100, 2\)  
                        ELSE 0   
                    END AS AchievementPct  
                FROM \[dbo\].\[vw\_RevenuePerformance\_Detail\]  
                WHERE (@FiscalYearId IS NULL OR FiscalYearId \= @FiscalYearId)  
                  AND (@ParentId IS NULL OR ParentId \= @ParentId)  
                  AND (@PlanItemId IS NULL OR PlanItemId \= @PlanItemId)  
                GROUP BY MonthName, MonthOrder  
                ORDER BY MonthOrder ASC;";

            using var conn \= GetConnection();  
            var rows \= (await conn.QueryAsync\<dynamic\>(sql, new {   
                filters.FiscalYearId,   
                filters.ParentId,   
                filters.PlanItemId   
            })).ToList();

            return new MonthlyTrendDto  
            {  
                Months \= rows.Select(r \=\> (string)r.MonthName).ToList(),  
                Achievements \= rows.Select(r \=\> (decimal)(r.AchievementPct ?? 0m)).ToList()  
            };  
        }

        // 3\. Branch Performance Ranking (%)  
        public async Task\<BranchRankingDto\> GetBranchRankingAsync(DashboardFilterDto filters)  
        {  
            const string sql \= @"  
                SELECT   
                    ParentName AS BranchName,  
                    CASE   
                        WHEN SUM(TotalTarget) \> 0 THEN ROUND((SUM(TotalActual) / SUM(TotalTarget)) \* 100, 2\)  
                        ELSE 0   
                    END AS AchievementPct  
                FROM \[dbo\].\[vw\_RevenuePerformance\_Detail\]  
                WHERE (@FiscalYearId IS NULL OR FiscalYearId \= @FiscalYearId)  
                  AND (@PlanItemId IS NULL OR PlanItemId \= @PlanItemId)  
                GROUP BY ParentName  
                ORDER BY AchievementPct ASC;";

            using var conn \= GetConnection();  
            var rows \= (await conn.QueryAsync\<dynamic\>(sql, new {   
                filters.FiscalYearId,   
                filters.PlanItemId   
            })).ToList();

            return new BranchRankingDto  
            {  
                Branches \= rows.Select(r \=\> (string)r.BranchName).ToList(),  
                Scores \= rows.Select(r \=\> (decimal)(r.AchievementPct ?? 0m)).ToList()  
            };  
        }

        // 4\. Dynamic Filter Dropdown Options  
        public async Task\<FilterOptionsDto\> GetFilterOptionsAsync()  
        {  
            const string sqlYears \= "SELECT DISTINCT FiscalYearId AS Id, FiscalYearName \+ ' (በጀት ዓመት)' AS Name FROM \[dbo\].\[vw\_RevenuePerformance\_Detail\] ORDER BY FiscalYearId DESC;";  
            const string sqlBranches \= "SELECT DISTINCT ParentId AS Id, ParentName AS Name FROM \[dbo\].\[vw\_RevenuePerformance\_Detail\] ORDER BY ParentName ASC;";  
            const string sqlTaxTypes \= "SELECT DISTINCT PlanItemId AS Id, PlanItemName AS Name FROM \[dbo\].\[vw\_RevenuePerformance\_Detail\] ORDER BY PlanItemName ASC;";

            using var conn \= GetConnection();  
            var years \= await conn.QueryAsync\<FilterItem\>(sqlYears);  
            var branches \= await conn.QueryAsync\<FilterItem\>(sqlBranches);  
            var taxTypes \= await conn.QueryAsync\<FilterItem\>(sqlTaxTypes);

            return new FilterOptionsDto  
            {  
                FiscalYears \= years.ToList(),  
                Branches \= branches.ToList(),  
                TaxTypes \= taxTypes.ToList()  
            };  
        }  
    }  
}

## **5\. Phase 4: REST Controller Implementation**

Create Controllers/OperationsDashboardController.cs:

using Microsoft.AspNetCore.Mvc;  
using RevenueOperationsDashboard.Models;  
using RevenueOperationsDashboard.Services;

namespace RevenueOperationsDashboard.Controllers  
{  
    \[ApiController\]  
    \[Route("api/dashboard")\]  
    public class OperationsDashboardController : ControllerBase  
    {  
        private readonly DashboardRepository \_repo;

        public OperationsDashboardController(DashboardRepository repo)  
        {  
            \_repo \= repo;  
        }

        \[HttpGet("tax-type-performance")\]  
        public async Task\<IActionResult\> GetTaxTypePerformance(\[FromQuery\] DashboardFilterDto filters)  
            \=\> Ok(await \_repo.GetTaxTypePerformanceAsync(filters));

        \[HttpGet("monthly-trend")\]  
        public async Task\<IActionResult\> GetMonthlyTrend(\[FromQuery\] DashboardFilterDto filters)  
            \=\> Ok(await \_repo.GetMonthlyTrendAsync(filters));

        \[HttpGet("branch-ranking")\]  
        public async Task\<IActionResult\> GetBranchRanking(\[FromQuery\] DashboardFilterDto filters)  
            \=\> Ok(await \_repo.GetBranchRankingAsync(filters));

        \[HttpGet("filter-options")\]  
        public async Task\<IActionResult\> GetFilterOptions()  
            \=\> Ok(await \_repo.GetFilterOptionsAsync());  
    }  
}

## **6\. Phase 5: Dynamic Frontend Wiring (wwwroot/index.html)**

Copy the static HTML prototype into wwwroot/index.html and update its JavaScript logic to perform dynamic API fetches:

1. **Remove Hardcoded Payload:** Delete const rawDatabasePayload \= \[...\].  
2. **Implement Async Fetching Engine:**

// Dynamic API Fetch Pipeline  
async function refreshAllDashboardMetrics() {  
  const query \= new URLSearchParams({  
    fiscalYearId: state.filters.fiscalYear,  
    parentId: state.filters.parent,  
    planItemId: state.filters.planItem  
  }).toString();

  // Parallel REST requests  
  const \[taxRes, trendRes, branchRes\] \= await Promise.all(\[  
    fetch(\`/api/dashboard/tax-type-performance?${query}\`).then(r \=\> r.json()),  
    fetch(\`/api/dashboard/monthly-trend?${query}\`).then(r \=\> r.json()),  
    fetch(\`/api/dashboard/branch-ranking?${query}\`).then(r \=\> r.json())  
  \]);

  renderTaxTypeChart(taxRes);  
  renderMonthlyTrendChart(trendRes);  
  renderBranchRankingChart(branchRes);  
}

// Populate Filter Modal Dropdowns on Page Load  
async function loadFilterDropdowns() {  
  const res \= await fetch('/api/dashboard/filter-options');  
  const options \= await res.json();

  const yearSelect \= document.getElementById('filterFiscalYear');  
  yearSelect.innerHTML \= options.fiscalYears.map(y \=\> \`\<option value="${y.id}"\>${y.name}\</option\>\`).join('');

  const branchSelect \= document.getElementById('filterParent');  
  branchSelect.innerHTML \= \`\<option value="ALL"\>ሁሉም ቅርንጫፎች (All Branches)\</option\>\` \+   
    options.branches.map(b \=\> \`\<option value="${b.id}"\>${b.name}\</option\>\`).join('');

  const taxSelect \= document.getElementById('filterPlanItem');  
  taxSelect.innerHTML \= \`\<option value="ALL"\>ሁሉም የታክስ ዓይነቶች (All Tax Types)\</option\>\` \+   
    options.taxTypes.map(t \=\> \`\<option value="${t.id}"\>${t.name}\</option\>\`).join('');  
}

## **7\. Execution Checklist for AI Agent**

* \[ \] Build solution using dotnet build and ensure zero errors.  
* \[ \] Confirm Dapper connection executes without timeout issues against vw\_RevenuePerformance\_Detail.  
* \[ \] Verify swagger endpoints (/swagger) return valid JSON payloads for all 4 endpoints.  
* \[ \] Verify wwwroot/index.html renders full-screen canvas ECharts charts on slides 0, 1, and 2\.  
* \[ \] Verify auto-slide rotator interval continues running cleanly without memory leaks or zero-width canvas rendering issues.