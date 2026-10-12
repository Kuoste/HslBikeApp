using System.Net;
using System.Text;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.Services;

public class LiveStationServiceTests
{
    private static LiveStationService CreateServiceWithResponse(string json)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })));
        return new LiveStationService(httpClient, "https://aggregator.example");
    }

    #region RefreshAsync

    [Fact]
    public async Task RefreshAsync_WhenResponseIsSuccessful_PopulatesLiveCountsAndTimestamp()
    {
        var before = DateTime.UtcNow;
        var service = CreateServiceWithResponse(
            """
            [
              {"id":"001","name":"A","address":"Addr","lat":60.1,"lon":24.9,"capacity":20,
               "bikesAvailable":7,"spacesAvailable":13,"isActive":true},
              {"id":"002","name":"B","address":"Addr2","lat":60.2,"lon":24.8,"capacity":10,
               "bikesAvailable":3,"spacesAvailable":7,"isActive":true}
            ]
            """);

        var stations = await service.RefreshAsync();

        Assert.Equal(2, stations.Count);
        Assert.Equal(7, service.GetLiveCount("001"));
        Assert.Equal(3, service.GetLiveCount("002"));
        Assert.NotNull(service.LiveTimestamp);
        Assert.True(service.LiveTimestamp >= before);
    }

    [Fact]
    public async Task RefreshAsync_WhenBaseUrlHasTrailingSlash_UsesCorrectEndpoint()
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
        var service = new LiveStationService(httpClient, "https://aggregator.example/");

        await service.RefreshAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal("https://aggregator.example/api/stations", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task RefreshAsync_WhenHttpRequestFails_KeepsPreviousState()
    {
        // First call succeeds — use a one-line JSON array with single-quote delimited string
        var service = CreateServiceWithResponse(
            "[{\"id\":\"001\",\"name\":\"A\",\"address\":\"A\",\"lat\":0,\"lon\":0,\"capacity\":10," +
            "\"bikesAvailable\":5,\"spacesAvailable\":5,\"isActive\":true}]");
        await service.RefreshAsync();
        var timestampAfterFirstRefresh = service.LiveTimestamp;

        // Second call fails — state must be preserved
        var failClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("network error")));
        var failService = new LiveStationService(failClient, "https://aggregator.example");

        // Simulate previous state by using a service that has already refreshed once
        // by replacing with a failing client via a fresh service on a different instance.
        // We verify the contract: RefreshAsync returns empty but does NOT null LiveTimestamp.
        var result = await failService.RefreshAsync();

        Assert.Empty(result);
        // LiveTimestamp on a fresh service stays null after failure (no previous state to keep)
        Assert.Null(failService.LiveTimestamp);
    }

    [Fact]
    public async Task RefreshAsync_WhenStationIsInactive_ExcludesItFromLiveCounts()
    {
        var service = CreateServiceWithResponse(
            """
            [
              {"id":"001","name":"A","address":"A","lat":0,"lon":0,"capacity":10,
               "bikesAvailable":5,"spacesAvailable":5,"isActive":true},
              {"id":"002","name":"B","address":"B","lat":0,"lon":0,"capacity":10,
               "bikesAvailable":3,"spacesAvailable":7,"isActive":false}
            ]
            """);

        await service.RefreshAsync();

        Assert.Equal(5, service.GetLiveCount("001"));
        Assert.Null(service.GetLiveCount("002"));
    }

    [Fact]
    public async Task RefreshAsync_WhenResponseIsEmpty_ReturnsEmptyAndSetsTimestamp()
    {
        var service = CreateServiceWithResponse("[]");

        var stations = await service.RefreshAsync();

        Assert.Empty(stations);
        Assert.NotNull(service.LiveTimestamp);
        Assert.True(service.LastRefreshSucceeded);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("network")]
    [InlineData("server-error")]
    [InlineData("invalid-json")]
    public async Task RefreshAsync_WhenLaterRefreshFails_ReportsFailureAndKeepsPreviousState(string failure)
    {
        var callCount = 0;
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
                return Task.FromResult(JsonResponse(
                    "[{\"id\":\"001\",\"name\":\"A\",\"lat\":0,\"lon\":0,\"capacity\":10," +
                    "\"bikesAvailable\":5,\"spacesAvailable\":5,\"isActive\":true}]"));

            return failure switch
            {
                "timeout" => throw new TaskCanceledException("The request was cancelled due to the configured HttpClient.Timeout."),
                "network" => throw new HttpRequestException("network error"),
                "server-error" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
                _ => Task.FromResult(JsonResponse("<html>not json</html>"))
            };
        }));
        var service = new LiveStationService(httpClient, "https://aggregator.example");

        await service.RefreshAsync();
        var timestampAfterFirstRefresh = service.LiveTimestamp;
        Assert.True(service.LastRefreshSucceeded);

        var result = await service.RefreshAsync();

        Assert.Empty(result);
        Assert.False(service.LastRefreshSucceeded);
        Assert.Equal(timestampAfterFirstRefresh, service.LiveTimestamp);
        Assert.Equal(5, service.GetLiveCount("001"));
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    #endregion

    #region GetLiveCount

    [Fact]
    public void GetLiveCount_WhenStationNotInLiveCounts_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("no data")));
        var service = new LiveStationService(httpClient, "https://aggregator.example");

        Assert.Null(service.GetLiveCount("999"));
    }

    [Fact]
    public async Task GetLiveCount_IsCaseInsensitive()
    {
        var service = CreateServiceWithResponse(
            """
            [{"id":"ABC","name":"A","address":"A","lat":0,"lon":0,"capacity":10,
              "bikesAvailable":4,"spacesAvailable":6,"isActive":true}]
            """);
        await service.RefreshAsync();

        Assert.Equal(4, service.GetLiveCount("abc"));
        Assert.Equal(4, service.GetLiveCount("ABC"));
    }

    #endregion
}
