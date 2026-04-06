using System.Net.Http.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

public class StationService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public StationService(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<List<BikeStation>> FetchStationsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/stations");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<BikeStation>>() ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
