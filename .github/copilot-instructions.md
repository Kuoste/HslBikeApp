# HslBikeApp — Copilot Instructions

## System Overview

Helsinki city bike availability app. Two repositories form the full system:

| Repo | Role | Tech | Hosting |
|---|---|---|---|
| **HslBikeApp** (this repo) | Blazor WASM frontend | .NET 10, Blazor WebAssembly, Leaflet.js | GitHub Pages (`kuoste.github.io/HslBikeApp/`) |
| **HslBikeDataAggregator** | C# backend service | .NET 10, Azure Functions (isolated worker) | Azure Functions (Consumption plan) |

## Architecture

```
┌──────────────────────────┐       REST/JSON        ┌─────────────────────────────┐
│  HslBikeApp              │ ◄────────────────────── │  HslBikeDataAggregator      │
│  Blazor WASM             │                         │  Azure Functions             │
│  (GitHub Pages)          │                         │  (holds HSL API key 🔒)     │
└──────────────────────────┘                         └──────────┬──────────────────┘
                                                                │
                                                     ┌──────────▼──────────────────┐
                                                     │  HSL Digitransit GraphQL    │
                                                     │  HSL Open History Data      │
                                                     └─────────────────────────────┘
```

- The **aggregator** holds the Digitransit API key as a secret in Azure Functions app settings.
- The aggregator uses **write/read separation**: a timer function polls HSL every 2 min and writes to Azure Blob Storage; HTTP functions read from blob for sub-second responses.

### Hybrid Fallback (Cold Start Mitigation)

The frontend uses **progressive loading** to avoid blocking on aggregator cold start:

1. **Immediate**: fetch station data directly from HSL Digitransit (no backend dependency).
2. **Background**: call the aggregator for enriched data (snapshots, trends).
3. **Progressive**: hourly graphs and popular destinations load when the aggregator responds.

Basic station view is never blocked by the aggregator. See `docs/adr/001-azure-functions-backend.md` for full rationale.

## API Contract (HslBikeDataAggregator endpoints)

- `GET /api/stations` — current bike availability for all stations
- `GET /api/stations/{id}/availability` — aggregated hourly availability profile (graph data)
- `GET /api/stations/{id}/destinations` — popular destinations from HSL open history data
- `GET /api/snapshots` — recent snapshots for trend calculation (arrows up/down)

## Key Shared Models

- `BikeStation` — id, name, lat/lon, capacity, bikesAvailable, spacesAvailable, isActive
- `StationSnapshot` — timestamp + dictionary of stationId → bikeCount
- `StationHistory` — departure/arrival station pair, tripCount, avg duration/distance

## Frontend Architecture (this repo)

- **State**: singleton `AppState` with `OnStateChanged` event; components subscribe in `OnInitialized`/`OnAfterRenderAsync` and unsubscribe on dispose.
- **Services**: `StationService`, `HistoryService`, `SnapshotService`, `CycleLaneService` — each takes `HttpClient` via constructor.
- **Map**: Leaflet.js via JS interop (`wwwroot/js/map-interop.js`), driven by `MapView.razor`.
- **Pages**: single-page app with `Home.razor` as the main page.

## Conventions

- Records for immutable data models, classes for services and state.
- File-scoped namespaces, nullable enabled, implicit usings.
- Services return empty collections (never null) on failure.
- `ReadFromJsonAsync<T>()` for JSON deserialization.
- No direct HSL API calls from the frontend — always go through the aggregator.

## Development Workflow

- Use a **branch-per-issue** workflow for development.
- Create tests for existing functionality later (not immediately).
- Utilize **Architecture Decision Records (ADRs)** to document significant architectural decisions.
- Implement **pre-commit hooks** to enforce coding standards and run tests before commits.

## Planned Features

- **User geolocation**: show user's position on the map, sort/filter stations by proximity.
- **Cycle lane layer revision**: replace current Yleiskaava main route data with actual up-to-date cycle infrastructure from Helsinki WFS, with smart rendering for large datasets.
- **Hourly availability graph**: click a station to see typical bike counts throughout the day.
- **Popular destinations**: show where people typically ride to from a given station.
