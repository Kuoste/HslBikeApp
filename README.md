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
- **HslBikeDataAggregator** — C# Azure Functions backend ([separate repo](https://github.com/Kuoste/HslBikeDataAggregator)); provides stations, snapshots, hourly availability profiles, and popular destinations. Holds the Digitransit API key.
- **Digitransit API** — called directly by the frontend as an immediate fallback while the aggregator warms up (hybrid cold-start mitigation)
- **GitHub Actions** — CI/CD; `tools/FetchSnapshot` snapshot poller runs until aggregator snapshot endpoint is wired in

### Data flow

```
Page load (immediate)   ──► Digitransit API ──► station markers on map
Page load (background)  ──► HslBikeDataAggregator
                                ├─ /api/stations          ─► live availability
                                ├─ /api/snapshots         ─► trend arrows
                                ├─ /api/stations/{id}/availability  ─► hourly graph
                                └─ /api/stations/{id}/destinations  ─► popular destinations
```

Basic station view is never blocked by the aggregator's cold start. See [`docs/adr/001-azure-functions-backend.md`](docs/adr/001-azure-functions-backend.md) for full rationale.

## Setup

1. Clone the repo
2. For local development, create `src/HslBikeApp/wwwroot/appsettings.Development.json` and set your Digitransit subscription key there:
   ```json
   { "DigitransitSubscriptionKey": "your-key-here" }
   ```
   `appsettings.Development.json` is gitignored and overrides values from `appsettings.json` when running locally with the `Development` environment.
3. Run locally:
   ```bash
   dotnet run --project src/HslBikeApp
   ```
   In VS Code, the default debug profile now uses a plain run-once launch. Use the separate watch profile only when you want hot reload.
4. Run tests:
   ```bash
   dotnet test
   ```

## Seasonal behaviour

HSL city bikes are seasonal. Outside the operating season, the upstream live station feed can legitimately return zero active stations. When that happens, the app shows an explicit status message instead of leaving the map blank without explanation.

## API Key

Get a free API key from the [Digitransit API portal](https://portal-api.digitransit.fi/).  
The key is used for rate-limiting — it is a public transit API, not a secret.

For GitHub Actions, add `DIGITRANSIT_SUBSCRIPTION_KEY` as a repository secret.

## Data Sources and Attribution

- Live station data comes from the Digitransit API. Digitransit states that the data is available under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The app attributes the data to Digitransit and shows the live retrieval timestamp in the UI.
- Map tiles and map data come from [OpenStreetMap](https://www.openstreetmap.org/copyright). OpenStreetMap data is licensed under ODbL and is attributed in the map UI.

## License

The source code in this repository is licensed under the [MIT License](LICENSE).

## Project Structure

```
src/HslBikeApp/          — Blazor WASM app
tools/FetchSnapshot/      — Console app for GH Actions snapshot poller
tests/HslBikeApp.Tests/   — xUnit + bUnit tests
.github/workflows/        — CI/CD + snapshot poller
docs/adr/                 — Architecture Decision Records
```
