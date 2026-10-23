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
    "DefaultChartMetricMode": "pct",
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
          "DurationMs": 20000
        },
        {
          "Id": "slide-3",
          "Visible": true,
          "Title": "YoY Monthly Trend Comparison",
          "TitleAm": "የገቢ አፈፃፀም በወራት ንፅፅር",
          "Subtitle": "Monthly Achievement Trend Comparison with Previous Year",
          "SubtitleAm": "ከቀዳሚው ዓመት ጋር የወርሃዊ አፈፃፀም ንፅፅር",
          "ApiEndpoint": "/api/dashboard/yoy-monthly-trend",
          "DurationMs": 20000
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
| `DefaultChartMetricMode` | string | `"pct"`, `"amount"`, `"both"` | Default metric display mode for charts: percentage (%), ETB amount, or both combined. |
| `DefaultTheme` | string | `"ocean"`, `"emerald"`, `"national"`, `"forest"`, `"sunset"`, `"grayscale"` | Visual color theme for backgrounds, bars, curves, and cards. |

#### Calculation Mode Detail
- `"ToDateTarget"`: Computes quotas proportional to days elapsed in the current month plus closed previous months (የዕስከ ዛሬ ዕቅድ).
- `"AnnualTarget"`: Evaluates current actual collections against the full 12-month annual quota (የሙሉ ዓመት ዕቅድ).

#### Chart Metric Mode Detail
- `"pct"`: Shows whole percentages (e.g. `104%`), with 100% target baseline lines.
- `"amount"`: Formatted whole ETB amounts with HumanNumbers (e.g. `ብር 25ቢ`, `ብር 700ሚ`, `ETB 25B`).
- `"both"`: Combined display (e.g. `ብር 25ቢ (104%)`, or dual-axis bars and lines).

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

#### Available Slide IDs

1. **`slide-0`**: Tax Type Performance (Plan vs Actual bar comparison).
2. **`slide-1`**: Monthly Achievement Trend (Cumulative monthly curve).
3. **`slide-3`**: YoY Monthly Trend Comparison (Current year vs prior year).
4. **`slide-2`**: Branch Office Achievement (Ranking of branch offices).
5. **`slide-4`**: Daily Revenue Trend (Daily collection timeline trajectory).

> **Tip**: You can change the order of slide objects in the JSON array to change the order they appear on screen!

---

## 4. Practical Configuration Recipes

### Recipe 1: Boardroom Executive Review (Full Year Quota & Both Metrics)
To display annual targets with both currency amounts and achievement percentages in the Emerald theme:

```json
  "DashboardSettings": {
    "DefaultLanguage": "am",
    "KpiCalculationMode": "AnnualTarget",
    "DefaultChartMetricMode": "both",
    "DefaultTheme": "emerald",
    "Carousel": {
      "AutoPlay": true,
      "IntervalMs": 25000
    }
  }
```

### Recipe 2: Daily Operations Center (To-Date Targets & Pure Currency)
To track daily progress against elapsed days with actual ETB revenue amounts:

```json
  "DashboardSettings": {
    "DefaultLanguage": "am",
    "KpiCalculationMode": "ToDateTarget",
    "DefaultChartMetricMode": "amount",
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

## 6. Carousel Rendering & Smooth Animation Architecture

### Zero Blank Delay with Full Entrance Animation
In earlier versions, navigating to an unvisited slide caused a momentary blank black container while the HTTP request fetched data from the server.
In **v2.2.5**, this is solved through a dual-stage architecture:

1. **Background JSON Preloading (`preloadAllSlides`)**:
   Immediately after initial dashboard load (or filter application), the client issues background fetches for all configured slides into memory (`state.activeSlideData[idx]`). It does *not* render the canvas in the dark, preserving the initial chart state.

2. **On-Demand Animation Replay (`renderActiveSlideChart(index, data, replayAnimation = true)`)**:
   When the user or auto-rotator switches to a slide:
   - The slide container is brought into view (`slide-visible`).
   - The chart renders immediately from in-memory cache in 0ms (no empty black box or network delay).
   - ECharts executes its full entrance animation (spline drawing, bar growth, luminous wick ascension) dynamically in front of the viewer.
   - When switching metric modes via `[በመቶኛ (%)]` and `[በብር (Wicks)]`, `replayAnimation = false` ensures an in-place smooth series transition without clearing the chart.

