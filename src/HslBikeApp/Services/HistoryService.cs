using System.Net;
using System.Net.Http.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

public class HistoryService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public HistoryService(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<List<StationHistory>> FetchHistoryAsync(string stationId)
    {
        var url = $"{_baseUrl}/api/stations/{Uri.EscapeDataString(stationId)}/destinations";

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

        var list = await response.Content.ReadFromJsonAsync<List<StationHistory>>() ?? [];
        list.Sort((a, b) => b.TripCount.CompareTo(a.TripCount));
        return list;
    }
}
