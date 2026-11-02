using System.Net;
using System.Text;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.Services;

public class StatisticsServiceTests
{
    [Fact]
    public async Task FetchStatisticsAsync_WhenResponseIsSuccessful_ReturnsDeserialisedStatistics()
    {
        var responseJson =
            """
            {
              "month": "2026-06",
              "demand": {
                "departuresByHour": [0,0,0,0,0,1,5,12,20,15,10,8,7,6,8,12,18,22,15,8,4,2,1,0],
                "arrivalsByHour":   [0,0,0,0,0,0,3,10,18,14,9,7,6,5,7,10,16,20,14,7,3,1,0,0],
                "weekdayDeparturesByHour": [0,0,0,0,0,1,6,15,25,18,12,9,8,7,9,14,20,25,17,9,5,2,1,0],
                "weekendDeparturesByHour": [0,0,0,0,0,0,2,5,8,8,7,6,5,4,5,7,10,12,10,6,3,1,0,0],
                "weekdayArrivalsByHour":   [0,0,0,0,0,0,4,12,22,17,11,8,7,6,8,12,18,23,16,8,4,1,0,0],
                "weekendArrivalsByHour":   [0,0,0,0,0,0,1,4,7,7,6,5,4,3,4,6,9,11,9,5,2,1,0,0]
              },
              "destinations": {
                "fields": ["arrivalStationId", "tripCount", "averageDurationSeconds", "averageDistanceMetres"],
                "rows": [
                  ["002", 150, 480, 1200],
                  ["003", 80, 600, 1800]
                ]
              }
            }
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            })));
        var service = new StatisticsService(httpClient, "https://aggregator.example");

        var result = await service.FetchStatisticsAsync("001");

        Assert.NotNull(result);
        Assert.Equal("2026-06", result.Month);
        Assert.Equal(24, result.Demand.DeparturesByHour.Length);
        Assert.Equal(2, result.Destinations.Rows.Count);
    }

    [Fact]
    public async Task FetchStatisticsAsync_WhenEndpointReturns404_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var service = new StatisticsService(httpClient, "https://aggregator.example");

        var result = await service.FetchStatisticsAsync("999");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchStatisticsAsync_WhenHttpRequestFails_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("boom")));
        var service = new StatisticsService(httpClient, "https://aggregator.example");

        var result = await service.FetchStatisticsAsync("001");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchStatisticsAsync_UsesCorrectEndpointUrl()
    {
        HttpRequestMessage? capturedRequest = null;

        var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }));
        var service = new StatisticsService(httpClient, "https://aggregator.example/");

        await service.FetchStatisticsAsync("001");

        Assert.NotNull(capturedRequest);
        Assert.Equal("https://aggregator.example/api/stations/001/statistics", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public void Constructor_WhenBaseUrlIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new StatisticsService(new HttpClient(), null!));
    }

    [Fact]
    public void Constructor_WhenBaseUrlIsEmpty_AllowsOfflineMode()
    {
        var exception = Record.Exception(() => new StatisticsService(new HttpClient(), ""));

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StatisticsService(null!, "https://example.com"));
    }

    [Fact]
    public async Task FetchStatisticsAsync_WhenStationIdIsEmpty_ThrowsArgumentException()
    {
        var service = new StatisticsService(new HttpClient(), "https://aggregator.example");

        await Assert.ThrowsAsync<ArgumentException>(() => service.FetchStatisticsAsync(""));
    }
}
