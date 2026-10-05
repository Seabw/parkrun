# Parkrun Consolidated Club Results Scraper (.NET 9 / C#)

A C# .NET 9 console application that scrapes and extracts **Parkrun Consolidated Club Results** across all worldwide parkrun events for **Birmingham Swifts (Club #21925)** (or any other club) and automatically generates a styled **PDF report** directly in your `~/Downloads` folder.

---

## Features

- **Weekly Club Milestones Tracking**:
  - Automatically identifies member running milestones (official 25, 50, 100, 250, 500, 1000 clubs, junior 10 club, and celebrated club milestones like 150th, 200th, 300th, etc.) and volunteer credit milestones.
  - Prominently showcases milestone achievers at the start of the report with celebratory cards highlighting their milestone tier, event, finish time, and PB badges.
  - Highlights milestone pills directly beside member names in the results table.
  - Persists all club milestone achievements historically in `data/milestones.json`.
- **Weekly Trend Tracking & Graphing**:
  - Automatically records historical weekly stats in `data/history.json`.
  - Dynamically renders an embedded multi-week participation and attendance trend chart using `ScottPlot` with ample vertical height (170pt) and generous top headroom to ensure all data labels are clear and never touch the top border.
- **Weekly Club Volunteers & Event Rosters**:
  - Automatically scrapes event-level volunteer rosters across all attended parkruns to capture both running and non-running volunteers (e.g. Run Directors, Marshals, Timekeepers).
  - Displays exclusively the active volunteers for that specific weekend.
  - Details their assigned role(s) that week, event attended, official milestone badges (`V25`, `V50`, `V100`, `V250`), and lifetime volunteer credits.
  - Caches profiles in `data/volunteers.json` for fast, rate-limit-resilient generation.
- **Clean 2-Page Standard Report**:
  - **Page 1**: Weekly Milestones Section (when milestones occur), followed by Club Runners Table with enlarged typography (13pt headers, 13pt pos, 13pt runner, 13pt time, 15pt finishers, 11.5pt event) and milestone badges. Interactive hyperlinks on runner names.
  - **Page 2**: Taller Weekly Trends Graph, Weekly Volunteers Table (13pt/15pt typography), and Celebrating Our Volunteers appreciation banner (13pt).
- **Fast & Modern**: Built on .NET 9, `HtmlAgilityPack`, `QuestPDF`, and `ScottPlot`.

---

## Quick Start

### 1. Run with default configuration (Generates PDF for Birmingham Swifts):
```bash
dotnet run --project /Users/bowang/.gemini/antigravity-ide/scratch/parkrun-scraper
```

### 2. Specifying a Different Club ID or URL:
Pass another club's numerical ID or full URL with `-c` or `--club`:
```bash
dotnet run --project /Users/bowang/.gemini/antigravity-ide/scratch/parkrun-scraper -- --club 21925
```

### 3. Specifying a Historical Date:
Extract results for a specific weekend event date (`YYYY-MM-DD`):
```bash
dotnet run --project /Users/bowang/.gemini/antigravity-ide/scratch/parkrun-scraper -- --date 2026-08-22
```

---

## Configuration (`appsettings.json`)

Pre-configured for Birmingham Swifts in [appsettings.json](appsettings.json):
```json
{
  "DefaultClubNum": "21925",
  "DefaultClubName": "Birmingham Swifts",
  "DownloadFolder": "~/Downloads",
  "OutputFilenamePattern": "parkrun_{0}_{1}.pdf",
  "SingleOutputFilename": "parkrun_club_results.pdf",
  "OverwriteSingleFile": false
}
```

---

## Pushing to GitHub as an Independent Repository

This project is configured as its own independent Git repository. To push to GitHub:

```bash
cd /Users/bowang/.gemini/antigravity-ide/scratch/parkrun-scraper
git push origin main
```
