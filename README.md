# Revenue Operations Dashboard

An enterprise-grade, high-performance revenue operations dashboard built on **ASP.NET Core (.NET 10)** and **Dapper**, featuring real-time financial KPI analytics, multi-language localization (English & Amharic), executive quota governance, and interactive visualization.

---

## Key Features

- **Executive Revenue Targets & Quota Governance**:
  - Dynamically computes revenue execution percentages against official government revenue quotas defined in `[dbo].[ExecutiveRevenuePlans]`.
  - Robust **dynamic target resolution**:
    1. *Primary*: Active executive target for the fiscal year (`[dbo].[ExecutiveRevenuePlans]`).
    2. *Fallback*: Current aggregated target from the revenue performance view (`[dbo].[vw_RevenuePerformance]`).
- **Governed HumanNumbers Localization (Bilingual)**:
  - Powered by the **HumanNumbers** formatting engine with custom magnitude suffix tables.
  - Formats monetary values and counts seamlessly for both **English** (`ETB 371.00B`, `884.65K`) and **Amharic** (`ብር 371.00ቢ`, `884.65ሺ`).
- **Real-Time KPI Cards & Multi-Chart Analytics**:
  - Top 6 Parallelized KPI Cards (Revenue Achievement %, Annual Filing, Renewal Clearances, VAT Declarations, Payroll Income Tax, Tax Audit Performance).
  - Tax Type Performance Breakdown (Grouped Bar Chart).
  - Monthly Achievement Trends vs 100% Target Baseline (Smooth Line Chart).
  - Year-Over-Year (YoY) Monthly Comparison Trends.
  - Branch Office Rankings and Volume Distribution.
- **Enterprise Performance**:
  - Parallel asynchronous SQL queries executing via Dapper for sub-second dashboard rendering.
  - In-memory caching for ultra-responsive user filtering across Fiscal Years, Parents, and Plan Items.
  - Dynamic full-screen presentation / carousel mode for operational displays.

---

## Tech Stack

- **Backend**: ASP.NET Core (.NET 10), C# 13, Dapper ORM, Microsoft.Data.SqlClient
- **Localization**: HumanNumbers (v2.0.2) with custom Ethiopic magnitude suffix arrays (`ሺ`, `ሚ`, `ቢ`, `ት`, `ኳድ`, `ኩዊ`)
- **Database**: Microsoft SQL Server (`dashboard_alpha_db`)
- **Frontend**: Responsive HTML5, Vanilla JavaScript, Apache ECharts, Tailwind CSS, FontAwesome

---

## Project Structure

```
RevenueOperationsDashboard/
├── Controllers/
│   └── OperationsDashboardController.cs   # API endpoints for filters, charts & KPI cards
├── Models/
│   └── DashboardDtos.cs                   # Strongly-typed DTOs for requests & responses
├── Services/
│   └── DashboardRepository.cs             # High-performance Dapper data access & KPI logic
├── wwwroot/                               # Static frontend assets
│   ├── css/                               # FontAwesome stylesheets
│   ├── webfonts/                          # Web font binaries
│   ├── echarts.min.js                     # Local ECharts library
│   ├── tailwindcss.js                     # Tailwind script
│   └── index.html                         # Interactive dashboard SPA
├── appsettings.json                       # Core app & carousel settings
├── appsettings.Development.json           # Development database connection
├── Program.cs                             # Startup configuration & HumanNumbers policy setup
└── RevenueOperationsDashboard.csproj      # .NET project definitions & package dependencies
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Microsoft SQL Server instance with `dashboard_alpha_db`

### Database Configuration

Verify your connection string in `RevenueOperationsDashboard/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=dashboard_alpha_db;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

### Running Locally

1. Navigate to the project directory:
   ```bash
   cd RevenueOperationsDashboard
   ```

2. Restore dependencies and launch with hot reload:
   ```bash
   dotnet watch
   ```

3. Open your browser and navigate to:
   ```
   http://localhost:5008
   ```

---

## API Endpoints

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/dashboard/top-cards` | Top KPI cards with localized formatted currency and counts |
| `GET` | `/api/dashboard/tax-type-performance` | Breakdown of target vs actual across tax types (in billions) |
| `GET` | `/api/dashboard/monthly-trend` | Monthly achievement percentage against baseline |
| `GET` | `/api/dashboard/yoy-monthly-trend` | Current vs previous fiscal year monthly comparison |
| `GET` | `/api/dashboard/branch-ranking` | Branch performance rankings |
| `GET` | `/api/dashboard/branch-volumes` | Branch revenue volume distributions |
| `GET` | `/api/dashboard/filters` | Dynamic fiscal year, parent, and plan item filter options |

---

## License

Internal & proprietary to the Revenue Operations Management team.
