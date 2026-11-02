using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

public class OpenDataService
{
    private readonly HttpClient _http;
    private readonly string _openDataUrl;

    public bool LastFetchSucceeded { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public OpenDataService(HttpClient http, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(http);

        // An empty URL is allowed: it means no backend is running and AppState shows an offline notice.
        if (baseUrl is null)
            throw new ArgumentException("Aggregator base URL must be configured.", nameof(baseUrl));

        _http = http;
        _openDataUrl = $"{baseUrl.TrimEnd('/')}/api/open-data";
    }

    public async Task<List<OpenDataTimeSeries>> FetchOpenDataAsync()
    {
        LastFetchSucceeded = false;
        LastErrorMessage = null;

        try
        {
            var response = await _http.GetAsync(_openDataUrl);
            response.EnsureSuccessStatusCode();

            LastFetchSucceeded = true;
            return await response.Content.ReadFromJsonAsync<List<OpenDataTimeSeries>>() ?? [];
        }
        catch (HttpRequestException ex)
        {
            LastErrorMessage = CreateHttpErrorMessage(ex.StatusCode);
            return [];
        }
        catch (JsonException)
        {
            LastErrorMessage = $"Could not load open data from the aggregator backend. GET {_openDataUrl} did not return valid JSON.";
            return [];
        }
    }

    private string CreateHttpErrorMessage(HttpStatusCode? statusCode)
    {
        if (statusCode is HttpStatusCode.NotFound)
            return $"Could not load open data from the aggregator backend. GET {_openDataUrl} returned 404 Not Found. Check AggregatorBaseUrl in wwwroot/appsettings.json.";

        if (statusCode is not null)
            return $"Could not load open data from the aggregator backend. GET {_openDataUrl} returned {(int)statusCode} {statusCode}.";

        return $"Could not load open data from the aggregator backend. GET {_openDataUrl} failed.";
    }
}
