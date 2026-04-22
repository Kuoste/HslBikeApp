using System.Net;
using System.Text;
using HslBikeApp.Models;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.Services;

public class SnapshotServiceTests
{
    private static SnapshotService CreateServiceWithResponse(string json)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })));
        return new SnapshotService(httpClient, "https://aggregator.example");
    }

    private static SnapshotService CreateServiceWithFetch(string json)
    {
        var service = CreateServiceWithResponse(json);
        service.FetchSnapshotsAsync().GetAwaiter().GetResult();
        return service;
    }

    #region FetchSnapshotsAsync

    [Fact]
    public async Task FetchSnapshotsAsync_WhenResponseIsSuccessful_ReturnsDeserialisedTimeSeries()
    {
        var responseJson =
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z", "2026-06-01T10:15:00Z"],
              "rows": [
                ["001", 5, 8],
                ["002", 3, 6]
              ]
            }
            """;

        var service = CreateServiceWithResponse(responseJson);

        var result = await service.FetchSnapshotsAsync();

        Assert.NotNull(result);
        Assert.Equal(15, result.IntervalMinutes);
        Assert.Equal(2, result.Timestamps.Count);
        Assert.Equal(2, result.RawRows.Count);
    }

    [Fact]
    public async Task FetchSnapshotsAsync_WhenBaseUrlHasTrailingSlash_UsesCorrectEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;

        var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"intervalMinutes":15,"timestamps":[],"rows":[]}""",
                    Encoding.UTF8, "application/json")
            });
        }));
        var service = new SnapshotService(httpClient, "https://aggregator.example/");

        await service.FetchSnapshotsAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal("https://aggregator.example/api/snapshots", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task FetchSnapshotsAsync_WhenHttpRequestFails_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("boom")));
        var service = new SnapshotService(httpClient, "https://aggregator.example");

        var result = await service.FetchSnapshotsAsync();

        Assert.Null(result);
    }

    [Fact]
    public void LatestSnapshotTimestamp_WhenFetched_ReturnsLastTimestamp()
    {
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z", "2026-06-01T10:15:00Z"],
              "rows": [["001", 5, 8]]
            }
            """);

        Assert.Equal(DateTime.Parse("2026-06-01T10:15:00Z").ToUniversalTime(), service.LatestSnapshotTimestamp);
    }

    [Fact]
    public void LatestSnapshotTimestamp_WhenNoDataFetched_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("no data")));
        var service = new SnapshotService(httpClient, "https://aggregator.example");

        Assert.Null(service.LatestSnapshotTimestamp);
    }

    #endregion

    #region GetStationCounts

    [Fact]
    public void GetStationCounts_WhenStationExists_ReturnsCounts()
    {
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z", "2026-06-01T10:15:00Z"],
              "rows": [["001", 5, 8], ["002", 3, 6]]
            }
            """);

        var counts = service.GetStationCounts("001");

        Assert.Equal([5, 8], counts);
    }

    [Fact]
    public void GetStationCounts_WhenStationNotFound_ReturnsEmpty()
    {
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z"],
              "rows": [["001", 5]]
            }
            """);

        var counts = service.GetStationCounts("999");

        Assert.Empty(counts);
    }

    [Fact]
    public void GetStationCounts_IsCaseInsensitive()
    {
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z"],
              "rows": [["ABC", 5]]
            }
            """);

        var counts = service.GetStationCounts("abc");

        Assert.Equal([5], counts);
    }

    #endregion

    #region GetTrend / GetTrendSummary — no live data

    [Fact]
    public void GetTrend_WhenNoData_ReturnsStable()
    {
        var service = CreateServiceWithFetch(
            """{"intervalMinutes":15,"timestamps":[],"rows":[]}""");

        Assert.Equal(AvailabilityTrend.Stable, service.GetTrend("001"));
    }

    [Fact]
    public void GetTrendSummary_WhenNoData_ReturnsStableWithZeroDeltaAndWindow()
    {
        var service = CreateServiceWithFetch(
            """{"intervalMinutes":15,"timestamps":[],"rows":[]}""");

        Assert.Equal(new TrendSummary(AvailabilityTrend.Stable, 0, 0), service.GetTrendSummary("001"));
    }

    [Fact]
    public void GetTrend_WhenNoLiveData_AndBikesDecreasingRapidly_ReturnsRapidDecrease()
    {
        // Two snapshots 1 min apart, −4 bikes → rate −4/min ≤ −2 → RapidDecrease
        var now = DateTime.UtcNow;
        var json = $$"""
            {
              "intervalMinutes": 1,
              "timestamps": ["{{now.AddMinutes(-1):o}}", "{{now:o}}"],
              "rows": [["001", 10, 6]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        Assert.Equal(AvailabilityTrend.RapidDecrease, service.GetTrend("001"));
    }

    [Fact]
    public void GetTrend_WhenNoLiveData_AndBikesStable_ReturnsStable()
    {
        var now = DateTime.UtcNow;
        var json = $$"""
            {
              "intervalMinutes": 15,
              "timestamps": ["{{now.AddMinutes(-15):o}}", "{{now:o}}"],
              "rows": [["001", 10, 10]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        Assert.Equal(AvailabilityTrend.Stable, service.GetTrend("001"));
    }

    [Fact]
    public void GetTrendSummary_WhenWindowIsLessThanOneMinute_ReturnsStableWithZeroWindow()
    {
        var now = DateTime.UtcNow;
        var json = $$"""
            {
              "intervalMinutes": 1,
              "timestamps": ["{{now:o}}", "{{now.AddSeconds(30):o}}"],
              "rows": [["001", 5, 7]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        Assert.Equal(new TrendSummary(AvailabilityTrend.Stable, 0, 0), service.GetTrendSummary("001"));
    }

    #endregion

    #region GetTrendSummary — with live data

    [Fact]
    public void GetTrendSummary_WithLive_WhenLiveIsWithin10MinOfLastSnapshot_UsesPrevSnapshotVsLive()
    {
        // Last snapshot T-2min, prev snapshot T-17min.
        // Live is 2 min after last snapshot → within 10 min → use prev (T-17) vs live.
        // Prev count = 4, live count = 10 → delta = +6 over ~19 min → rate ~0.32/min → Stable.
        var now = DateTime.UtcNow;
        var tPrev = now.AddMinutes(-17);
        var tLast = now.AddMinutes(-2);
        var tLive = now;
        var json = $$"""
            {
              "intervalMinutes": 15,
              "timestamps": ["{{tPrev:o}}", "{{tLast:o}}"],
              "rows": [["001", 4, 8]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        var summary = service.GetTrendSummary("001", liveCount: 10, liveTimestamp: tLive);

        // baseline is prev snapshot (count=4, T-17min); live count=10
        Assert.Equal(6, summary.DeltaBikes);
        Assert.InRange(summary.WindowMinutes, 16, 18);
    }

    [Fact]
    public void GetTrendSummary_WithLive_WhenLiveIsMoreThan10MinAfterLastSnapshot_UsesLastSnapshotVsLive()
    {
        // Last snapshot T-12min. Live is 12 min after → ≥ 10 min → use last vs live.
        // Last count = 5, live count = 15 → delta = +10 over 12 min → rate ~0.83/min → Increasing.
        var now = DateTime.UtcNow;
        var tPrev = now.AddMinutes(-27);
        var tLast = now.AddMinutes(-12);
        var tLive = now;
        var json = $$"""
            {
              "intervalMinutes": 15,
              "timestamps": ["{{tPrev:o}}", "{{tLast:o}}"],
              "rows": [["001", 3, 5]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        var summary = service.GetTrendSummary("001", liveCount: 15, liveTimestamp: tLive);

        Assert.Equal(AvailabilityTrend.Increasing, summary.Trend);
        Assert.Equal(10, summary.DeltaBikes);
        Assert.InRange(summary.WindowMinutes, 11, 13);
    }

    [Fact]
    public void GetTrendSummary_WithLive_WhenOnlyOneSnapshot_UsesSnapshotVsLive()
    {
        var now = DateTime.UtcNow;
        var tLast = now.AddMinutes(-20);
        var json = $$"""
            {
              "intervalMinutes": 15,
              "timestamps": ["{{tLast:o}}"],
              "rows": [["001", 5]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        var summary = service.GetTrendSummary("001", liveCount: 5, liveTimestamp: now);

        Assert.Equal(AvailabilityTrend.Stable, summary.Trend);
        Assert.Equal(0, summary.DeltaBikes);
    }

    [Fact]
    public void GetTrendSummary_WithLive_WhenNoSnapshots_ReturnsStable()
    {
        var service = CreateServiceWithFetch(
            """{"intervalMinutes":15,"timestamps":[],"rows":[]}""");

        var summary = service.GetTrendSummary("001", liveCount: 8, liveTimestamp: DateTime.UtcNow);

        Assert.Equal(new TrendSummary(AvailabilityTrend.Stable, 0, 0), summary);
    }

    [Fact]
    public void GetTrendSummary_WithLive_WhenLiveCloseToLastSnapshot_ReturnsStableForSlowRate()
    {
        // Live within 10 min of last snapshot → falls back to S_prev vs live.
        // Window ≈ 18 min, delta = 7 − 0 = 7, rate ≈ 0.39/min → below 0.5 threshold → Stable.
        var now = DateTime.UtcNow;
        var tPrev = now.AddMinutes(-18);
        var tLast = now.AddMinutes(-3);
        var json = $$"""
            {
              "intervalMinutes": 15,
              "timestamps": ["{{tPrev:o}}", "{{tLast:o}}"],
              "rows": [["001", 0, 5]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        var summary = service.GetTrendSummary("001", liveCount: 7, liveTimestamp: now);

        Assert.Equal(AvailabilityTrend.Stable, summary.Trend);
        Assert.Equal(7, summary.DeltaBikes);
        Assert.InRange(summary.WindowMinutes, 17, 19);
    }

    #endregion

    #region IsGap

    [Fact]
    public void IsGap_WhenTimestampsAreConsecutive_ReturnsFalse()
    {
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z", "2026-06-01T10:15:00Z"],
              "rows": [["001", 5, 8]]
            }
            """);

        Assert.False(service.IsGap(1));
    }

    [Fact]
    public void IsGap_WhenLargeGapExists_ReturnsTrue()
    {
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z", "2026-06-01T12:00:00Z"],
              "rows": [["001", 5, 8]]
            }
            """);

        Assert.True(service.IsGap(1));
    }

    #endregion
}
