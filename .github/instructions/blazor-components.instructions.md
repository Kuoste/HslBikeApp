---
applyTo: "**/*.razor"
---
# Blazor Component Guidelines

- Use `@inject` for service injection, not constructor injection.
- Subscribe to `AppState.OnStateChanged` via `StateHasChanged` in `OnInitializedAsync` or `OnAfterRenderAsync(firstRender)`.
- Always unsubscribe: use `@implements IDisposable` (sync) or `@implements IAsyncDisposable` (when JS interop cleanup needed).
- For JS interop, use `IJSRuntime` and call via `await JS.InvokeVoidAsync("MapInterop.methodName", ...)`.
- Keep `@code` blocks focused — extract complex logic to services or `AppState`.
- Use `InvokeAsync(() => ...)` when calling `StateHasChanged` from non-UI threads (e.g., event handlers).
- Leaflet map interactions go through `wwwroot/js/map-interop.js` — don't create parallel JS files.
