---
applyTo: "**/Services/*.cs"
---
# Service Guidelines

- All HTTP services take `HttpClient` and a base URL string via constructor injection.
- The backend base URL comes from `IConfiguration["AggregatorBaseUrl"]` — never hardcode it.
- Return empty collections (not null) on HTTP failure — catch `HttpRequestException`, return `[]`.
- Use `ReadFromJsonAsync<T>()` for JSON deserialisation.
- When C# property names differ from JSON keys, use `[JsonPropertyName]` attributes.
- Services should not hold UI state — that belongs in `AppState`.
- All data fetching goes through the aggregator REST API, not directly to external APIs.
