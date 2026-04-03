---
applyTo: "**/Services/*.cs"
---
# Service Guidelines

- All HTTP services take `HttpClient` via constructor injection.
- Return empty collections (not null) on HTTP failure — catch `HttpRequestException`, return `[]`.
- Use `ReadFromJsonAsync<T>()` for JSON deserialization.
- Services should not hold UI state — that belongs in `AppState`.
- When the aggregator backend is available, all data fetching should go through its REST API, not directly to external APIs.
