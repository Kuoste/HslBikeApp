using System.Net;
using System.Text;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.Services;

public class StationServiceTests
{
    [Fact]
    public async Task FetchStationsAsync_WhenResponseIsSuccessful_ReturnsDeserialisedStations()
    {
        var responseJson =
            """
            [
              {
                "id": "001",
                "name": "Kaivopuisto",
                "lat": 60.155,
                "lon": 24.95,
                "capacity": 20,
                "bikesAvailable": 5,
                "spacesAvailable": 15,
                "isActive": true
              }
            ]
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        })));
        var service = new StationService(httpClient, "https://aggregator.example");

        var stations = await service.FetchStationsAsync();

        var station = Assert.Single(stations);
        Assert.Equal("001", station.Id);
        Assert.Equal("Kaivopuisto", station.Name);
        Assert.Equal(60.155, station.Latitude);
        Assert.Equal(24.95, station.Longitude);
        Assert.Equal(20, station.Capacity);
        Assert.Equal(5, station.BikesAvailable);
        Assert.Equal(15, station.SpacesAvailable);
        Assert.True(station.IsActive);
        Assert.Equal(string.Empty, station.Address);
        Assert.Null(station.LastUpdated);
        Assert.True(service.LastFetchSucceeded);
        Assert.Null(service.LastErrorMessage);
    }

    [Fact]
    public async Task FetchStationsAsync_WhenBaseUrlHasTrailingSlash_UsesAggregatorStationsEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;

        var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }));
        var service = new StationService(httpClient, "https://aggregator.example/");

        await service.FetchStationsAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal("https://aggregator.example/api/stations", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task FetchStationsAsync_WhenHttpRequestFails_ReturnsEmptyList()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => throw new HttpRequestException("boom")));
        var service = new StationService(httpClient, "https://aggregator.example");

        var stations = await service.FetchStationsAsync();

        Assert.Empty(stations);
        Assert.False(service.LastFetchSucceeded);
        Assert.Equal("Could not load live bike stations from the aggregator backend. GET https://aggregator.example/api/stations failed.", service.LastErrorMessage);
    }

    [Fact]
    public async Task FetchStationsAsync_WhenEndpointReturnsNotFound_SetsConfigurationErrorMessage()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var service = new StationService(httpClient, "https://aggregator.example");

        var stations = await service.FetchStationsAsync();

        Assert.Empty(stations);
        Assert.False(service.LastFetchSucceeded);
        Assert.Equal("Could not load live bike stations from the aggregator backend. GET https://aggregator.example/api/stations returned 404 Not Found. Check AggregatorBaseUrl in wwwroot/appsettings.json.", service.LastErrorMessage);
    }
}
