# HslBikeApp — Copilot Instructions

> **This file is the canonical source of instructions for this repository.** `CLAUDE.md` at the repository root imports this file — edit here, not in `CLAUDE.md`.

## System Overview

Helsinki city bike availability app. Two repositories form the full system:

| Repo | Role | Tech | Hosting |
|---|---|---|---|
| **HslBikeApp** (this repo) | Blazor WASM frontend | .NET 10, Blazor WebAssembly, Leaflet.js | GitHub Pages (kuoste.github.io/HslBikeApp/) |
| **HslBikeDataAggregator** | REST backend service | .NET 10, Azure Functions (isolated worker) | Interchangeable — currently Azure Functions |

## Architecture

`
┌──────────────────────────┐       REST/JSON        ┌─────────────────────────────┐
│  HslBikeApp              │ ◄────────────────────── │  REST backend               │
│  Blazor WASM             │                         │  (configurable via          │
│  (GitHub Pages)          │                         │   AggregatorBaseUrl)        │
└──────────────────────────┘                         └──────────┬──────────────────┘
                                                                │
                                                     ┌──────────▼──────────────────┐
                                                     │  HSL Digitransit GraphQL    │
                                                     │  HSL Open History Data      │
                                                     └─────────────────────────────┘
`

- The frontend **never** calls HSL APIs directly — all data flows through the REST backend.
- The backend base URL is configured via AggregatorBaseUrl in wwwroot/appsettings.json. The backend is interchangeable: any REST API implementing the same contract can be used.
- The current backend implementation (HslBikeDataAggregator) uses write/read separation: a timer polls HSL and writes to blob storage; HTTP functions read from blob for sub-second responses. See docs/adr/001-azure-functions-backend.md for rationale.

## API Contract (HslBikeDataAggregator endpoints)

- GET /api/stations — current live bike availability for all stations
- GET /api/snapshots — columnar snapshot time series for trend calculation
- GET /api/stations/{id}/statistics — monthly statistics: hourly availability profile + popular destinations
- GET /api/open-data — open data sources (e.g. venue fill levels)

## Key Shared Models

- `BikeStation` — id, name, address, lat/lon, capacity, bikesAvailable, spacesAvailable, isActive, lastUpdated; computed: occupancy, isEmpty, isFull
- `SnapshotTimeSeries` — columnar time series: intervalMinutes, timestamps[], rows[] (each row: [stationId, count0, count1, …])
- `StationCountSeries` — stationId + int[] counts aligned with timestamps
- `MonthlyStationStatistics` — monthly station statistics including demand profile and destination table
- `DemandProfile` — hourly availability data (hour 0–23 → averageBikesAvailable)
- `DestinationRow` / `DestinationTable` — popular destinations from HSL open history data
- `TrendSummary` / `TrendThresholds` / `AvailabilityTrend` — trend calculation models
- `OpenDataTimeSeries` — sourceId, displayName, lat, lon, attributionUrl, optional unit, optional description, timestamps[], values[] (double; `-1` = unavailable/out of season). `LatestAvailable()` returns the most recent non-sentinel sample.
- `CycleLane` — cycle lane geometry

## Frontend Architecture (this repo)

- **State**: singleton AppState with OnStateChanged event; components subscribe in OnInitialized/OnAfterRenderAsync and unsubscribe on dispose.
- **Services**: `StationService`, `LiveStationService`, `SnapshotService`, `StatisticsService`, `OpenDataService`, `CycleLaneService` — each takes HttpClient via constructor. `LiveStationService` maintains a live (timestamp, counts) pair separate from the snapshot history. `OpenDataService` fetches generic open data time series rendered through `OpenDataDetailPanel`.
- **Map**: Leaflet.js via JS interop (wwwroot/js/map-interop.js), driven by MapView.razor.
- **Pages**: single-page app with Home.razor as the main page.

## Conventions

- Records for immutable data models, classes for services and state.
- File-scoped namespaces, nullable enabled, implicit usings.
- Services return empty collections (never null) on failure.
- ReadFromJsonAsync<T>() for JSON deserialisation.
- No direct HSL API calls from the frontend — always go through the aggregator backend.
- When C# property names differ from JSON keys, use [JsonPropertyName] attributes.
- Use British English consistently in identifiers, including function, method, variable, parameter, and local naming where practical, while preserving required external API, framework, library, and contract names.

## Delivery Workflow

- Always start by pulling the latest from the remote (`git pull`) before beginning any work to prevent merge conflicts.
- Keep implementation work tied to an open GitHub issue.
- Use an issue branch named issue-<number>-<short-description> for delivery.
- If an issue was closed before its code was pushed, reopen the issue before continuing work.
- Add or update automated tests for each delivered behaviour or repository-level configuration change.
- Run dotnet build HslBikeApp.slnx and then dotnet test HslBikeApp.slnx before considering the issue complete.
- Every issue must include new or updated unit tests covering the delivered behaviour.
- Do not treat an issue as done until the branch is pushed, the pull request is open, and CI is passing.
- Explicitly link pull requests to their GitHub issue using closing keywords such as Closes #<issue> to ensure the issue is automatically closed when the PR is merged.
- Continue to use Architecture Decision Records (ADRs) for significant architectural decisions.
- Keep pre-commit hooks in .githooks/ in place to enforce coding standards and run checks before commits.

## Language Preferences

- Use British English consistently in responses, code comments, documentation, commit and pull request text, and GitHub content.
- Avoid non-English or stray foreign text in responses.

## Remaining Issues

Open issues as of 2026-05-18 — phases 1–3 are complete:

- **#5** (phase:4-independent): Show user location on the map. No backend dependency.
- **#6** (phase:4-independent): Revise cycle lane layer with actual infrastructure data. No backend dependency.
- **#8** (phase:5-housekeeping): Normalise naming to British English across the repository. Do last to avoid conflicts.

## Trend Calculation Guidelines

- When calculating trends, do not rely solely on the few minutes since the latest live refresh when the snapshot interval is 15 minutes; instead, compare against the full relevant interval (e.g. about 18 minutes in this case).
