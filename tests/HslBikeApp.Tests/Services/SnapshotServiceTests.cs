using System.Net;
using System.Text;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.Services;

public class SnapshotServiceTests
{
    [Fact]
    public async Task FetchSnapshotsAsync_WhenResponseIsSuccessful_ReturnsDeserialisedSnapshots()
    {
        var responseJson =
            """
            [
              {
                "timestamp": "2026-04-03T12:00:00+03:00",
                "bikeCounts": {
                  "001": 5,
                  "002": 3
                }
              }
            ]
            """;
        var expectedUtcTimestamp = DateTimeOffset.Parse("2026-04-03T12:00:00+03:00").UtcDateTime;

        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        })));
        var service = new SnapshotService(httpClient, "https://aggregator.example");

        var snapshots = await service.FetchSnapshotsAsync();

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(expectedUtcTimestamp, snapshot.Timestamp.ToUniversalTime());
        Assert.Equal(5, snapshot.BikeCounts["001"]);
        Assert.Equal(3, snapshot.BikeCounts["002"]);
    }

    [Fact]
    public async Task FetchSnapshotsAsync_WhenBaseUrlHasTrailingSlash_UsesAggregatorSnapshotsEndpoint()
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
        var service = new SnapshotService(httpClient, "https://aggregator.example/");

        await service.FetchSnapshotsAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal("https://aggregator.example/api/snapshots", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task FetchSnapshotsAsync_WhenHttpRequestFails_ReturnsEmptyList()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => throw new HttpRequestException("boom")));
        var service = new SnapshotService(httpClient, "https://aggregator.example");

        var snapshots = await service.FetchSnapshotsAsync();

        Assert.Empty(snapshots);
    }
}
