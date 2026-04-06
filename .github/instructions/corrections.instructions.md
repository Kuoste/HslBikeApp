---
applyTo: "**"
---

# Corrections & Lessons Learned

These are documented corrections from past AI-assisted development sessions.
Copilot should follow these rules to avoid repeating mistakes.

## File Placement

- `.github/copilot-instructions.md` and `.github/instructions/` MUST be at the **repository root**, NOT inside project folders like `src/HslBikeApp/.github/`.
- Always verify file paths relative to the repo root before creating instruction files.

## GitHub CLI (`gh`)

- `gh milestone create` does **not** exist. Use `gh api repos/{owner}/{repo}/milestones` with `--method POST` for milestone creation.
- Always verify `gh` subcommand availability before using it.

## Coordinate Systems

- Helsinki WFS returns coordinates as `[longitude, latitude]`.
- Leaflet expects `[latitude, longitude]`.
- Always flip coordinates when converting from WFS to Leaflet.

## .NET / Blazor

- Blazor WASM apps hosted on GitHub Pages require `<base href>` to match the repository name path (e.g., `/HslBikeApp/`).
- When renaming a repo, update the base href in `wwwroot/index.html` accordingly.

## JSON Serialisation

- `ReadFromJsonAsync<T>()` uses camelCase by default, which matches the aggregator API.
- When C# property names differ from JSON keys (e.g., `Latitude` vs `lat`), add `[JsonPropertyName("lat")]` — do not rely on naming conventions alone.
- Always verify the model properties match the documented API response shape before deserialising.
