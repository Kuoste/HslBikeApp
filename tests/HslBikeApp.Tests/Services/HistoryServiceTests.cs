using System.Net;
using System.Text;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.Services;

public class HistoryServiceTests
{
    [Fact]
    public async Task FetchHistoryAsync_WhenResponseIsSuccessful_ReturnsTripCountDescending()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson =
            """
            [
              {
                "departureStationId": "001",
                "arrivalStationId": "003",
                "tripCount": 12,
                "averageDurationSeconds": 180.5,
                "averageDistanceMetres": 780.2
              },
              {
                "departureStationId": "001",
                "arrivalStationId": "023",
                "tripCount": 42,
                "averageDurationSeconds": 360.5,
                "averageDistanceMetres": 1250.3
              }
            ]
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }));
        var service = new HistoryService(httpClient, "https://aggregator.example/");

        var history = await service.FetchHistoryAsync("001");

        Assert.NotNull(capturedRequest);
        Assert.Equal("https://aggregator.example/api/stations/001/destinations", capturedRequest.RequestUri?.ToString());
        Assert.Collection(
            history,
            first =>
            {
                Assert.Equal("023", first.ArrivalStationId);
                Assert.Equal(42, first.TripCount);
                Assert.Equal(360.5, first.AverageDurationSeconds);
                Assert.Equal(1250.3, first.AverageDistanceMetres);
            },
            second =>
            {
                Assert.Equal("003", second.ArrivalStationId);
                Assert.Equal(12, second.TripCount);
                Assert.Equal(180.5, second.AverageDurationSeconds);
                Assert.Equal(780.2, second.AverageDistanceMetres);
            });
    }

    [Fact]
    public async Task FetchHistoryAsync_WhenResponseIsNotFound_ReturnsEmptyList()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var service = new HistoryService(httpClient, "https://aggregator.example");

        var history = await service.FetchHistoryAsync("001");

        Assert.Empty(history);
    }

    [Fact]
    public async Task FetchHistoryAsync_WhenHttpRequestFails_ReturnsEmptyList()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => throw new HttpRequestException("boom")));
        var service = new HistoryService(httpClient, "https://aggregator.example");

        var history = await service.FetchHistoryAsync("001");

        Assert.Empty(history);
    }
}
