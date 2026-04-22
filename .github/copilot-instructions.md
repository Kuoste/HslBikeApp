# HslBikeApp — Copilot Instructions

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

- GET /api/stations — current bike availability for all stations
- GET /api/stations/{id}/availability — aggregated hourly availability profile (graph data)
- GET /api/stations/{id}/destinations — popular destinations from HSL open history data
- GET /api/snapshots — recent snapshots for trend calculation (arrows up/down)

## Key Shared Models

- BikeStation — id, name, lat/lon, capacity, bikesAvailable, spacesAvailable, isActive
- StationSnapshot — timestamp + dictionary of stationId → bikeCount
- StationHistory — departure/arrival station pair, tripCount, avg duration/distance
- HourlyAvailability — hour (0–23) + averageBikesAvailable

## Frontend Architecture (this repo)

- **State**: singleton AppState with OnStateChanged event; components subscribe in OnInitialized/OnAfterRenderAsync and unsubscribe on dispose.
- **Services**: StationService, HistoryService, SnapshotService, CycleLaneService — each takes HttpClient via constructor.
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

## Issue Ordering

The backlog is organised into phases. Respect dependencies when picking up work:

1. **Phase 1 — Migration** (#1, #2, #3): refactor services to call the aggregator. Independent of each other.
2. **Phase 2 — Cleanup** (#7): simplify Program.cs and remove legacy artefacts. Depends on phase 1.
3. **Phase 3 — Feature** (#4): hourly availability graph. Depends on aggregator URL being configured.
4. **Phase 4 — Independent** (#5, #6): geolocation and cycle lanes. No backend dependency.
5. **Phase 5 — Housekeeping** (#8): British English normalisation. Do after phase 2 to avoid conflicts.

## Trend Calculation Guidelines

- When calculating trends, do not rely solely on the few minutes since the latest live refresh when the snapshot interval is 15 minutes; instead, compare against the full relevant interval (e.g., about 18 minutes in this case).
