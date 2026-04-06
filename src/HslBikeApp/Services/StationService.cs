using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

public class StationService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _stationsUrl;

    public bool LastFetchSucceeded { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public StationService(HttpClient http, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(http);

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Aggregator base URL must be configured.", nameof(baseUrl));

        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _stationsUrl = $"{_baseUrl}/api/stations";
    }

    public async Task<List<BikeStation>> FetchStationsAsync()
    {
        LastFetchSucceeded = false;
        LastErrorMessage = null;

        try
        {
            var response = await _http.GetAsync(_stationsUrl);
            response.EnsureSuccessStatusCode();

            LastFetchSucceeded = true;
            return await response.Content.ReadFromJsonAsync<List<BikeStation>>() ?? [];
        }
        catch (HttpRequestException ex)
        {
            LastErrorMessage = CreateHttpErrorMessage(ex.StatusCode);
            return [];
        }
        catch (JsonException)
        {
            LastErrorMessage = $"Could not load live bike stations from the aggregator backend. GET {_stationsUrl} did not return valid JSON.";
            return [];
        }
    }

    private string CreateHttpErrorMessage(HttpStatusCode? statusCode)
    {
        if (statusCode is HttpStatusCode.NotFound)
            return $"Could not load live bike stations from the aggregator backend. GET {_stationsUrl} returned 404 Not Found. Check AggregatorBaseUrl in wwwroot/appsettings.json.";

        if (statusCode is not null)
            return $"Could not load live bike stations from the aggregator backend. GET {_stationsUrl} returned {(int)statusCode} {statusCode}.";

        return $"Could not load live bike stations from the aggregator backend. GET {_stationsUrl} failed.";
    }
}
