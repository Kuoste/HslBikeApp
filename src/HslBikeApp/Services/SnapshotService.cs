using System.Net;
using System.Net.Http.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

public class SnapshotService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public SnapshotService(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<List<StationSnapshot>> FetchSnapshotsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/snapshots");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<StationSnapshot>>() ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
