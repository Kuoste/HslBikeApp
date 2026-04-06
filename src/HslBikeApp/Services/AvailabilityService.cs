using System.Net;
using System.Net.Http.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

public class AvailabilityService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public AvailabilityService(HttpClient http, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(http);

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Aggregator base URL must be configured.", nameof(baseUrl));

        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<List<HourlyAvailability>> FetchAvailabilityAsync(string stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            throw new ArgumentException("Station ID must be provided.", nameof(stationId));

        var url = $"{_baseUrl}/api/stations/{Uri.EscapeDataString(stationId)}/availability";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url);
        }
        catch (HttpRequestException)
        {
            return [];
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<HourlyAvailability>>() ?? [];
    }
}
