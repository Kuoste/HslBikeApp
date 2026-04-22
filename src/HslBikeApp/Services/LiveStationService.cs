using System.Net.Http.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

/// <summary>
/// Fetches and caches the latest live bike counts from GET /api/stations.
/// Keeps a single (timestamp, counts) pair — no history retained.
/// </summary>
public class LiveStationService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public DateTime? LiveTimestamp { get; private set; }
    public IReadOnlyDictionary<string, int> LiveCounts { get; private set; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public LiveStationService(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Calls GET /api/stations and updates <see cref="LiveTimestamp"/> and <see cref="LiveCounts"/> atomically.
    /// Keeps previous state on <see cref="HttpRequestException"/> to avoid UI flicker.
    /// Returns the fetched stations, or an empty list on failure.
    /// </summary>
    public async Task<IReadOnlyList<BikeStation>> RefreshAsync()
    {
        try
        {
            var stations = await _http.GetFromJsonAsync<List<BikeStation>>($"{_baseUrl}/api/stations")
                           ?? [];

            LiveCounts = stations
                .Where(s => s.IsActive)
                .ToDictionary(s => s.Id, s => s.BikesAvailable, StringComparer.OrdinalIgnoreCase);
            LiveTimestamp = DateTime.UtcNow;

            return stations;
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>Returns the live bike count for a station, or <c>null</c> if not available.</summary>
    public int? GetLiveCount(string stationId) =>
        LiveCounts.TryGetValue(stationId, out var count) ? count : null;
}
