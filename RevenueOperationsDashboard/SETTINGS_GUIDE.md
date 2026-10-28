# Revenue Operations Dashboard - Settings & Configuration Guide

This guide provides a complete reference for configuring and managing the Revenue Operations Dashboard directly from your desk using [appsettings.json](file:///c:/Users/User/source/repos/OperationDashboard%20Project/RevenueOperationsDashboard/appsettings.json) and [appsettings.Development.json](file:///c:/Users/User/source/repos/OperationDashboard%20Project/RevenueOperationsDashboard/appsettings.Development.json).

No code changes or recompilations are needed to adjust defaults, slide sequences, themes, or calculation modes.

---

## 1. Configuration Hierarchy

The dashboard uses standard ASP.NET Core configuration layering:

1. **`appsettings.json`**: Base configuration used in production and default deployments.
2. **`appsettings.Development.json`**: Overrides base settings when running locally in Development mode (`ASPNETCORE_ENVIRONMENT=Development`).
3. **Environment Variables**: Can override any setting on server hosts (e.g. `DashboardSettings__KpiCalculationMode=AnnualTarget`).

When the browser loads the dashboard, the client calls `/api/dashboard/config` and initializes all UI elements and filters according to these settings.

---

## 2. Complete Configuration Reference

Here is the complete and cleaned schema currently supported:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=dashboard_prod_oct22;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;TrustServerCertificate=True;"
  },
  "AllowedHosts": "*",
  "DashboardSettings": {
    "DefaultLanguage": "am",
    "KpiCalculationMode": "ToDateTarget",
    "DefaultChartMetricMode": "wicks",
    "DefaultTheme": "ocean",
    "DefaultFilters": {
      "FiscalYearId": "2019",
      "ParentId": "ALL",
      "PlanItemId": "ALL"
    },
    "WeeklyReport": {
      "Enabled": true,
      "PeriodDays": 7,
      "TitleAm": "ሳምንታዊ የገቢ አፈፃፀም ሪፖርት",
      "TitleEn": "Executive Weekly Performance Report",
      "SubtitleAm": "የ 7 ቀናት ተከታታይ የስራ አፈፃፀም፣ የታክስ አይነቶች እና የቅርንጫፎች ደረጃ",
      "SubtitleEn": "Past 7 days performance, revenue streams, and branch rankings"
    },
    "Carousel": {
      "AutoPlay": true,
      "IntervalMs": 30000,
      "Slides": [
        {
          "Id": "slide-0",
          "Visible": true,
          "Title": "Tax Type Performance",
          "TitleAm": "የታክስ አይነት አፈፃፀም",
          "Subtitle": "Comparison of Plan Target vs Actual Performance",
          "SubtitleAm": "የእቅድ እና ክንውን ንፅፅር",
          "ApiEndpoint": "/api/dashboard/tax-type-performance",
          "DurationMs": 20000
        },
        {
          "Id": "slide-1",
          "Visible": true,
          "Title": "Monthly Achievement Trend",
          "TitleAm": "የወርሃዊ አፈፃፀም አዝማሚያ",
          "Subtitle": "Monthly Cumulative Achievement Trend % against 100% target baseline",
          "SubtitleAm": "ወርሃዊ ድምር አፈፃፀም ንፅፅር በመቶኛ",
          "ApiEndpoint": "/api/dashboard/monthly-trend",
          "DurationMs": 20000,
          "DefaultMetricMode": "wicks"
        },
        {
          "Id": "slide-3",
          "Visible": true,
          "Title": "YoY Monthly Trend Comparison",
          "TitleAm": "የገቢ አፈፃፀም በወራት ንፅፅር",
          "Subtitle": "Monthly Achievement Trend Comparison with Previous Year",
          "SubtitleAm": "ከቀዳሚው ዓመት ጋር የወርሃዊ አፈፃፀም ንፅፅር",
          "ApiEndpoint": "/api/dashboard/yoy-monthly-trend",
          "DurationMs": 20000,
          "DefaultMetricMode": "wicks"
        },
        {
          "Id": "slide-2",
          "Visible": true,
          "Title": "Branch Office Achievement",
          "TitleAm": "የቅርንጫፍ ጽህፈት ቤቶች አፈፃፀም",
          "Subtitle": "Comparing target achievement percentages across all branch offices",
          "SubtitleAm": "የቅርንጫፍ ጽህፈት ቤቶችን የእቅድ አፈፃፀም በመቶኛ ማነፃፀር",
          "ApiEndpoint": "/api/dashboard/branch-ranking",
          "DurationMs": 20000
        },
        {
          "Id": "slide-4",
          "Visible": true,
          "Title": "Daily Revenue Trend",
          "TitleAm": "ዕለታዊ የገቢ አፈፃፀም አዝማሚያ",
          "Subtitle": "Daily revenue collection trajectory in Billions ETB",
          "SubtitleAm": "ዕለታዊ የገቢ ስብስብ ሂደት በቢሊየን ብር",
          "ApiEndpoint": "/api/dashboard/daily-trend",
          "DurationMs": 20000
        }
      ]
    }
  }
}
```

---

## 3. Settings Breakdown

### 3.1 Global Presentation Defaults

| Setting | Type | Allowed Values | Description |
| :--- | :--- | :--- | :--- |
| `DefaultLanguage` | string | `"am"`, `"en"` | Primary language loaded on page startup. |
| `KpiCalculationMode` | string | `"ToDateTarget"`, `"AnnualTarget"` | Primary calculation baseline for top KPI cards and chart targets. |
| `DefaultChartMetricMode` | string | `"pct"`, `"wicks"` | Default metric display mode for the time-series curves (Slide 1 Monthly Trend & Slide 3 YoY Comparison). |
| `DefaultTheme` | string | `"ocean"`, `"emerald"`, `"national"`, `"forest"`, `"sunset"`, `"grayscale"` | Visual color theme for backgrounds, bars, curves, and cards. |

#### Calculation Mode Detail
- `"ToDateTarget"`: Computes quotas proportional to days elapsed in the current month plus closed previous months (የዕስከ ዛሬ ዕቅድ).
- `"AnnualTarget"`: Evaluates current actual collections against the full 12-month annual quota (የሙሉ ዓመት ዕቅድ).

#### Chart Metric Mode Detail
- Slide 0 (**Tax Type Performance**) natively renders side-by-side Plan vs Actual comparison bars in **Currency (ETB)** formatted with 0-decimal HumanNumbers (e.g., `ብር 25ቢ`).
- Slide 2 (**Branch Office Achievement**) natively renders horizontal branch ranking bars in **Achievement Percentage (%)** sorted against the 100% target dashed baseline.
- Slides 1 & 3 (**Monthly Trend** and **YoY Monthly Trend**) are founded on the smooth **Achievement Percentage (%)** curve and extend with optional **Wicks**:
  - `"pct"`: Clean percentage curve (`0% - 140%` scale) with 100% target baseline line and achievement labels above markers.
  - `"wicks"` (or `"amount"` / `"both"`): Retains the smooth percentage curve while sprouting slender luminous vertical wicks rising 15 units above active data points, capped with dark glassmorphic badges displaying formatted ETB collection values (`ብር ...`). Line percentage labels shift cleanly below markers to prevent overlap.

---

### 3.2 Default Filters (`DefaultFilters`)

| Setting | Type | Example | Description |
| :--- | :--- | :--- | :--- |
| `FiscalYearId` | string | `"2019"` | Default Ethiopian fiscal calendar year selected on launch. |
| `ParentId` | string | `"ALL"` or `"2"` | `"ALL"` shows all branch offices; a specific ID focuses the dashboard on one branch. |
| `PlanItemId` | string | `"ALL"` or GUID | `"ALL"` aggregates all revenue streams; a specific GUID filters to a single tax type. |

---

### 3.3 Executive Weekly Report (`WeeklyReport`)

| Setting | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Enabled` | boolean | `true` | When `false`, hides the executive report button in the header. |
| `PeriodDays` | integer | `7` | Length of the rolling reporting window ending on the current date. |
| `TitleAm` | string | `"ሳምንታዊ የገቢ አፈፃፀም ሪፖርት"` | Amharic modal report header title. |
| `TitleEn` | string | `"Executive Weekly Performance Report"` | English modal report header title. |
| `SubtitleAm` | string | - | Amharic subtitle text under the modal header. |
| `SubtitleEn` | string | - | English subtitle text under the modal header. |

> **Note on Removed Settings**: Obsolete properties `CutoffDayOfWeek` and `AutoShowOnCutoffDay` have been removed. The weekly report now calculates rolling sequential days using `PeriodDays` and anchors to the current operating date.

---

### 3.4 Carousel & Slides Orchestration (`Carousel`)

| Setting | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `AutoPlay` | boolean | `true` | Enables automatic slide transitions. |
| `IntervalMs` | integer | `30000` | Default duration per slide in milliseconds (30000 = 30 seconds). |
| `Slides` | array | list of slides | The sequence, visibility, and titles for each slide. |

#### Slide Object Properties

Each entry in `Slides` has the following properties:

| Property | Type | Description |
| :--- | :--- | :--- |
| `Id` | string | Element ID: `"slide-0"`, `"slide-1"`, `"slide-2"`, `"slide-3"`, `"slide-4"`. |
| `Visible` | boolean | Set to `false` to completely disable/skip this slide during presentations. |
| `Title` | string | English slide title shown above the chart. |
| `TitleAm` | string | Amharic slide title shown above the chart. |
| `Subtitle` | string | English explanatory subtitle. |
| `SubtitleAm` | string | Amharic explanatory subtitle. |
| `ApiEndpoint` | string | Backend controller route providing metrics for this slide. |
| `DurationMs` | integer | Custom display duration in milliseconds for this specific slide (e.g. `20000`). |
| `DefaultMetricMode` | string | Optional per-slide metric mode override (`"wicks"` or `"pct"`) for time-series slides. |

#### Available Slide IDs

1. **`slide-0`**: Tax Type Performance (Plan vs Actual bar comparison).
2. **`slide-1`**: Monthly Achievement Trend (Cumulative monthly curve).
3. **`slide-3`**: YoY Monthly Trend Comparison (Current year vs prior year).
4. **`slide-2`**: Branch Office Achievement (Ranking of branch offices).
5. **`slide-4`**: Daily Revenue Trend (Daily collection timeline trajectory).

> **Tip**: You can change the order of slide objects in the JSON array to change the order they appear on screen!

---

## 4. Practical Configuration Recipes

### Recipe 1: Boardroom Executive Review (Full Year Quota & Wicks Display)
To display annual targets with luminous currency wicks extending above achievement percentage curves in the Emerald theme:

```json
  "DashboardSettings": {
    "DefaultLanguage": "am",
    "KpiCalculationMode": "AnnualTarget",
    "DefaultChartMetricMode": "wicks",
    "DefaultTheme": "emerald",
    "Carousel": {
      "AutoPlay": true,
      "IntervalMs": 25000
    }
  }
```

### Recipe 2: Daily Operations Center (To-Date Targets & Pure Percentage Curves)
To track to-date quotas with clean percentage curves and 100% baseline:

```json
  "DashboardSettings": {
    "DefaultLanguage": "am",
    "KpiCalculationMode": "ToDateTarget",
    "DefaultChartMetricMode": "pct",
    "DefaultTheme": "ocean",
    "Carousel": {
      "AutoPlay": true,
      "IntervalMs": 20000
    }
  }
```

### Recipe 3: Focused 3-Slide Sequence
To hide the daily trend and YoY comparison and only cycle through Tax Types, Monthly Trend, and Branch Ranking:

```json
  "Slides": [
    { "Id": "slide-0", "Visible": true, "DurationMs": 25000 },
    { "Id": "slide-1", "Visible": true, "DurationMs": 25000 },
    { "Id": "slide-2", "Visible": true, "DurationMs": 25000 },
    { "Id": "slide-3", "Visible": false },
    { "Id": "slide-4", "Visible": false }
  ]
```

---

## 5. Summary of Obsolete Settings Cleaned Up

The following settings were removed because they are no longer used by the active architecture:

1. **`CutoffDayOfWeek`** (WeeklyReport): Replaced by active rolling-window calculation anchored to the current operating date.
2. **`AutoShowOnCutoffDay`** (WeeklyReport): Replaced by on-demand executive modal access via the header button.
3. **`ComparisonMode`** (Slides): Replaced by dedicated slide components.
4. **`ChartType`** (Slides): Replaced by dedicated renderer functions.

---

## 6. Interactive Controls & Chart Behaviors

### 6.1 Time-Series Wicks Extension (Slides 1 & 3)
- **Visual Design**: The Monthly Achievement Trend and YoY Monthly Trend charts feature a dual-layer visualization:
  - **Foundation**: A smooth cubic spline representing monthly cumulative achievement percentages (scaled 0% to 140%) anchored by an amber dashed 100% baseline mark line.
  - **Luminous Wicks**: When set to `"wicks"` (or when the user clicks **በብር (Wicks)**), slender 2px vertical stems rise 15 units above each active month's marker, capped by a dark glassmorphic badge displaying whole formatted ETB collection values (e.g. `ብር 25ቢ`, `ብር 21ቢ`, `ብር 1ቢ`).
  - **Dynamic Label Positioning**: When wicks are active, percentage labels reposition beneath the marker points to prevent visual overlap. Inactive and future months with 0% collections do not sprout wicks.
- **In-Slide Floating Pill**: Both Slide 1 and Slide 3 feature floating toggle pills at the top right:
  - `[በመቶኛ (%)]`: Switches the chart to a pure percentage curve.
  - `[በብር (Wicks)]`: Sprouts the vertical currency wicks and badges.
- **Hierarchy of Defaults**:
  1. Slide-level `DefaultMetricMode` inside `Carousel.Slides` (e.g. `"DefaultMetricMode": "wicks"`).
  2. Global `DefaultChartMetricMode` inside `DashboardSettings` (e.g. `"DefaultChartMetricMode": "wicks"`).
  3. Interactive user clicks during an active session immediately override the display mode in real time.

### 6.2 Native Non-Toggle Slides (Slides 0 & 2)
- **Slide 0 (Tax Type Performance)**: Renders side-by-side comparison bars for Plan Target vs Actual Collection in Billions of Birr with 0 decimals. Redundant `100%` target labels were eliminated to preserve financial clarity.
- **Slide 2 (Branch Office Achievement)**: Renders horizontal bars ranking branch offices by achievement percentage sorted ascending against the 100% dashed baseline.

### 6.3 Edge Navigation & Screen Inactivity
- **Floating Navigation Arrows**: Located at the left and right screen edges, vertically centered at 50% screen height. Hovering within the edge trigger zone smoothly reveals the glassmorphic circular chevron buttons.
- **Auto-Fade on Inactivity**: When no mouse or keyboard activity occurs for 15 seconds, the dashboard enters executive presentation mode, auto-hiding all navigation chevrons, floating pills, and cursor to provide an uncluttered display. Moving the mouse immediately restores all controls.
- **Keyboard Shortcuts**:
  - `Right Arrow` / `Spacebar`: Advance to the next slide.
  - `Left Arrow`: Return to the previous slide.
  - `P`: Pause / Resume carousel autoplay.
  - `Escape`: Close any open modal dialog (Global Filters or Executive Weekly Report).

---

## 7. SQLite Storage, KPI Card Overrides & IIS Configuration

### 7.1 Architecture & Mode Settings
The dashboard includes an embedded SQLite persistence engine ([SqliteOverrideService.cs](file:///c:/Users/User/source/repos/OperationDashboard%20Project/RevenueOperationsDashboard/Services/SqliteOverrideService.cs)) enabling department administrators to adjust executive target quotas and actual figures on top KPI summary cards in real time without altering the core SQL Server database.

#### Operating Modes (`TopCardsMode`)
- **`Auto`**: All top KPI summary cards calculate exclusively from live SQL Server database transactions.
- **`Hybrid`** (Default): Live SQL calculations are displayed by default, but any KPI card with an active manual override uses the customized quota/actual values.
- **`Manual`**: All enabled override values take precedence.

#### SQLite Concurrency & Resilience
- **Database File**: `dashboard_overrides.db` stored in `App_Data/`.
- **WAL Journaling**: `PRAGMA journal_mode = WAL;` enables concurrent read and write operations from multi-threaded IIS worker processes without database lock contention.
- **Busy Timeout**: `PRAGMA busy_timeout = 5000;` provides a 5-second lock queue before timeout.
- **Automatic Multi-Path Fallback**: If the site directory permissions restrict writes, the service automatically probes and falls back to `C:\ProgramData\RevenueOperationsDashboard\App_Data\` or `%TEMP%\RevenueOperationsDashboard\App_Data\`, ensuring saves never fail.
- **Native Driver**: `e_sqlite3.dll` is packaged directly in the application root to eliminate missing dependency errors on IIS hosts.

---

### 7.2 IIS Folder Permissions Configuration
When hosting the dashboard in Microsoft IIS under an Application Pool identity (such as `RevenueOperations`), SQLite requires write, create, and delete permissions within the `App_Data` directory to generate the database and its supporting write-ahead log files (`.db-wal` and `.db-shm`).

Run the following command in PowerShell on the IIS production server:

```powershell
# Ensure App_Data folder exists
if (-not (Test-Path "C:\inetpub\wwwroot\revop\App_Data")) { 
    New-Item -ItemType Directory -Force -Path "C:\inetpub\wwwroot\revop\App_Data" 
}

# Grant full rights to the RevenueOperations Application Pool
icacls "C:\inetpub\wwwroot\revop\App_Data" /grant 'IIS AppPool\RevenueOperations:(OI)(CI)F' /T
```

> **PowerShell Quoting Rule**: In PowerShell, the permissions argument must be wrapped in single quotes (`'IIS AppPool\RevenueOperations:(OI)(CI)F'`). Without quotes, PowerShell interprets `(OI)` as a command expression and throws an error: `The term 'OI' is not recognized`.

Alternatively, in standard Windows Command Prompt (`cmd.exe`):
```cmd
icacls "C:\inetpub\wwwroot\revop\App_Data" /grant "IIS AppPool\RevenueOperations:(OI)(CI)F" /T
```

#### Meaning of Permission Flags
- `(OI)`: **Object Inherit** - Sub-files inherit these permissions.
- `(CI)`: **Container Inherit** - Sub-directories inherit these permissions.
- `F`: **Full Control** - Enables SQLite to read, write, create, lock, and purge temporary WAL/SHM log files.
- `/T`: Recursively applies permissions to all existing files inside the folder.

---

## 8. Release & Deployment Packaging

### 8.1 Automated Publishing Script (`publish.ps1`)
The dashboard includes an automated PowerShell publishing script located at the repository root:

```powershell
# Publish with interactive version prompt (defaults to next minor release)
.\publish.ps1

# Publish specific version with automated packaging and Git tagging
.\publish.ps1 -Version "v2.2.6" -TagGit -CreateZip
```

### 8.2 Release Artifacts
Each release generated by `publish.ps1` produces:
1. **Published Folder** (`./publish/<Version>/`): Standalone ASP.NET Core self-contained or framework-dependent application binaries with static web assets (`wwwroot/`) and configuration files.
2. **Settings Guide** (`./publish/<Version>/SETTINGS_GUIDE.md`): This documentation file is automatically copied into the root of every published output directory for immediate operational reference.
3. **Zip Archive** (`./publish/RevenueOperationsDashboard-<Version>.zip`): A compressed distribution package ready for immediate transfer and extraction onto production IIS or Kestrel servers.
4. **Git Tag**: Automatically tagged (e.g. `v2.2.6`) and pushed to the remote repository.

---

## 9. Version Changelog

| Version | Highlights & Changes |
| :--- | :--- |
| **v2.2.6** | Multi-directory SQLite fallback probe (`App_Data` -> `ProgramData` -> `TempPath`), WAL concurrency mode, 5000ms busy timeout, detailed diagnostic 500 error reporting, and native `e_sqlite3.dll` bundling for IIS. |
| **v2.2.5** | Dual-stage preloading and instant animation replay; non-blocking pause indicators; immediate carousel timing updates. |
| **v2.2.2** | Configured **በብር (Wicks)** as the default metric mode in `appsettings.json` and `appsettings.Development.json`; added per-slide `DefaultMetricMode` support in DTOs and client engine. |
| **v2.2.1** | Resolved edge navigation overlay hit-test conflict that intercepted mouse clicks on the top-right metric toggle pill. |
| **v2.2.0** | Introduced slender luminous **Wicks** extension on time-series charts (Slides 1 & 3) displaying ETB collections above percentage curves; restored pure currency to Tax Types and pure percentage to Branch Rankings. |
| **v2.1.0** | Executive Weekly Performance Report rolling calculation overhaul; cleanup of obsolete settings; addition of automated `publish.ps1` and `SETTINGS_GUIDE.md`. |

