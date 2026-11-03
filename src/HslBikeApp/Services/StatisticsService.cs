using System.Net;
using System.Net.Http.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

/// <summary>
/// Fetches monthly station statistics from <c>GET /api/stations/{id}/statistics</c>.
/// Replaces both the former HistoryService and AvailabilityService.
/// </summary>
public class StatisticsService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public StatisticsService(HttpClient http, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(http);

        // An empty URL is allowed: it means no backend is running and AppState shows an offline notice.
        if (baseUrl is null)
            throw new ArgumentException("Aggregator base URL must be configured.", nameof(baseUrl));

        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Fetches monthly statistics for a station.
    /// Returns <c>null</c> when the endpoint returns 404 or the request fails.
    /// </summary>
    public async Task<MonthlyStationStatistics?> FetchStatisticsAsync(string stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            throw new ArgumentException("Station ID must be provided.", nameof(stationId));

        var url = $"{_baseUrl}/api/stations/{Uri.EscapeDataString(stationId)}/statistics";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MonthlyStationStatistics>();
    }
}
