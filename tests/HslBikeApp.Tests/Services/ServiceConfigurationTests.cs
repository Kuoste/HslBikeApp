using System.Net;
using System.Text;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.Services;

/// <summary>
/// Validates that all aggregator-backed services enforce the base URL contract
/// used by Program.cs when wiring up DI with a single AggregatorBaseUrl.
/// </summary>
public class ServiceConfigurationTests
{
    private static readonly HttpClient SharedHttp = new();

    [Fact]
    public void StationService_WhenBaseUrlIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new StationService(SharedHttp, null!));
    }

    [Fact]
    public void StationService_WhenBaseUrlIsEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new StationService(SharedHttp, ""));
    }

    [Fact]
    public void StationService_WhenHttpClientIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StationService(null!, "https://example.com"));
    }

    [Theory]
    [InlineData("https://aggregator.example")]
    [InlineData("https://aggregator.example/")]
    public async Task AllAggregatorServices_WhenGivenSameBaseUrl_SendRequestsToSameHost(string baseUrl)
    {
        var capturedUris = new List<Uri>();

        var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            capturedUris.Add(request.RequestUri!);
            // Each service expects a different JSON shape
            var url = request.RequestUri?.AbsolutePath ?? "";
            var body = url switch
            {
                _ when url.Contains("/snapshots") => """{"intervalMinutes":15,"timestamps":[],"rows":[]}""",
                _ when url.Contains("/statistics") => """{"month":"2026-06","demand":{"departuresByHour":[],"arrivalsByHour":[],"weekdayDeparturesByHour":[],"weekendDeparturesByHour":[],"weekdayArrivalsByHour":[],"weekendArrivalsByHour":[]},"destinations":{"fields":[],"rows":[]}}""",
                _ => "[]"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }));

        var stationService = new StationService(httpClient, baseUrl);
        var snapshotService = new SnapshotService(httpClient, baseUrl);
        var statisticsService = new StatisticsService(httpClient, baseUrl);
        var liveStationService = new LiveStationService(httpClient, baseUrl);

        await stationService.FetchStationsAsync();
        await snapshotService.FetchSnapshotsAsync();
        await statisticsService.FetchStatisticsAsync("001");
        await liveStationService.RefreshAsync();

        Assert.Equal(4, capturedUris.Count);
        Assert.All(capturedUris, uri =>
        {
            Assert.Equal("aggregator.example", uri.Host);
            Assert.StartsWith("/api/", uri.AbsolutePath);
        });
    }

    [Fact]
    public void CycleLaneService_DoesNotRequireAggregatorBaseUrl()
    {
        // CycleLaneService uses its own WFS URL, not the aggregator base URL.
        // It must be constructable with just an HttpClient.
        var service = new CycleLaneService(SharedHttp);
        Assert.NotNull(service);
    }
}
