# HSL City Bikes

A Blazor WebAssembly app showing real-time Helsinki city bike station availability, trend tracking, cycle lane overlay, and historical trip data.

**Live:** https://kuoste.github.io/HslBikeApp/

## Features

- Real-time bike station availability
- Interactive Leaflet.js map with colour-coded station markers
- Availability trend arrows (bikes rented/returned since last snapshot)
- Change indicators showing bikes rented/returned between refreshes
- Station detail panel with popular trip destinations
- Hourly availability graph showing typical bike counts throughout the day
- Helsinki cycle lane overlay from open WFS data
- Dark mode support (follows OS preference)
- Auto-refresh every 30 seconds + manual refresh

## Architecture

- **Blazor WebAssembly** — standalone, hosted as static files on GitHub Pages
- **Leaflet.js** — raw JS interop for map rendering (OSM tiles)
- **HslBikeDataAggregator** — REST backend ([separate repo](https://github.com/Kuoste/HslBikeDataAggregator)); provides stations, snapshots, hourly availability profiles, and popular destinations
- **GitHub Actions** — CI/CD

All data flows through a single configurable REST backend (`AggregatorBaseUrl` in `wwwroot/appsettings.json`). The frontend never calls upstream data sources directly. See [`docs/adr/001-azure-functions-backend.md`](docs/adr/001-azure-functions-backend.md) for backend rationale.

### Data flow

```
Page load ──► REST backend (AggregatorBaseUrl)
                ├─ /api/stations                    ─► live availability
                ├─ /api/snapshots                   ─► trend arrows
                ├─ /api/stations/{id}/availability  ─► hourly graph
                └─ /api/stations/{id}/destinations  ─► popular destinations
```

## Setup

1. Clone the repo
2. Optionally override `AggregatorBaseUrl` for local development by creating `src/HslBikeApp/wwwroot/appsettings.Development.json`:
   ```json
   { "AggregatorBaseUrl": "https://your-local-or-dev-backend" }
   ```
   `appsettings.Development.json` is gitignored and overrides values from `appsettings.json`.
3. Run locally:
   ```bash
   dotnet run --project src/HslBikeApp
   ```
4. Run tests:
   ```bash
   dotnet test
   ```

## Seasonal behaviour

HSL city bikes are seasonal. Outside the operating season, the upstream live station feed can legitimately return zero active stations. When that happens, the app shows an explicit status message instead of leaving the map blank without explanation.

## Data Sources and Attribution

- Live station data is sourced from [HSL open data](https://www.hsl.fi/en/hsl/open-data) via the backend aggregator, available under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The app attributes the data to HSL and shows the live retrieval timestamp in the UI.
- Map tiles and map data come from [OpenStreetMap](https://www.openstreetmap.org/copyright). OpenStreetMap data is licensed under ODbL and is attributed in the map UI.

## License

The source code in this repository is licensed under the [MIT License](LICENSE).

## Project Structure

```
src/HslBikeApp/          — Blazor WASM app
tests/HslBikeApp.Tests/   — xUnit + bUnit tests
.github/workflows/        — CI/CD
docs/adr/                 — Architecture Decision Records
```
