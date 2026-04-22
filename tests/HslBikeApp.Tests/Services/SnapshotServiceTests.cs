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

    #region GetTrend

    [Fact]
    public void GetTrend_WhenNoData_ReturnsStable()
    {
        var service = CreateServiceWithFetch(
            """{"intervalMinutes":15,"timestamps":[],"rows":[]}""");

        Assert.Equal(AvailabilityTrend.Stable, service.GetTrend("001"));
    }

    [Fact]
    public void GetTrend_WhenBikesDecreasingRapidly_ReturnsRapidDecrease()
    {
        var timestamps = Enumerable.Range(0, 6)
            .Select(index => DateTime.UtcNow.AddMinutes(index).ToString("o"));
        var timestampsJson = "[" + string.Join(",", timestamps.Select(timestamp => $"\"{timestamp}\"")) + "]";
        var json = $$"""
            {
              "intervalMinutes": 1,
              "timestamps": {{timestampsJson}},
              "rows": [["001", 20, 16, 12, 8, 4, 0]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        Assert.Equal(AvailabilityTrend.RapidDecrease, service.GetTrend("001"));
    }

    [Fact]
    public void GetTrend_WhenBikesStable_ReturnsStable()
    {
        var timestamps = Enumerable.Range(0, 6)
            .Select(index => DateTime.UtcNow.AddMinutes(index * 5).ToString("o"));
        var timestampsJson = "[" + string.Join(",", timestamps.Select(timestamp => $"\"{timestamp}\"")) + "]";
        var json = $$"""
            {
              "intervalMinutes": 5,
              "timestamps": {{timestampsJson}},
              "rows": [["001", 10, 10, 10, 10, 10, 10]]
            }
            """;
        var service = CreateServiceWithFetch(json);

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
    public void GetTrendSummary_UsesTimeBasedWindowOfIntervalTimes1Point2ForWindowAndDelta()
    {
        // intervalMinutes=1 → targetWindow = 1.2 min.
        // Walk back from index 7: index 6 is only 1 min away (< 1.2); index 5 is 2 min away (>= 1.2).
        // So start = index 5, window = 2 min, delta = counts[7] - counts[5] = 5 - 3 = 2.
        var timestamps = Enumerable.Range(0, 8)
            .Select(index => DateTime.UtcNow.AddMinutes(index).ToString("o"));
        var timestampsJson = "[" + string.Join(",", timestamps.Select(timestamp => $"\"{timestamp}\"")) + "]";
        var json = $$"""
            {
              "intervalMinutes": 1,
              "timestamps": {{timestampsJson}},
              "rows": [["001", 99, 99, 0, 1, 2, 3, 4, 5]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        Assert.Equal(new TrendSummary(AvailabilityTrend.Increasing, 2, 2), service.GetTrendSummary("001"));
    }

    [Fact]
    public void GetTrendSummary_WhenLivePointAppended_UsesFullIntervalWindowFromOldestRelevantSnapshot()
    {
        // intervalMinutes=15 → targetWindow = 18 min.
        // Points: -18 min (count=0), -3 min (count=5), live now (count=7).
        // Walk back from live point: -3 min is only ~3 min away (< 18); -18 min is ~18 min away (>= 18).
        // Start = oldest point, window ≈ 18 min, delta = 7 - 0 = 7.
        var now = DateTime.UtcNow;
        var timestampsJson = $"[\"{now.AddMinutes(-18):o}\",\"{now.AddMinutes(-3):o}\"]";
        var json = $$"""
            {
              "intervalMinutes": 15,
              "timestamps": {{timestampsJson}},
              "rows": [["001", 0, 5]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        service.AppendLiveSnapshot(new Dictionary<string, int> { ["001"] = 7 });

        // rate = 7 bikes / 18 min ≈ 0.39/min → below 0.5 threshold → Stable,
        // but the window and delta must span the full ~18-min interval.
        var summary = service.GetTrendSummary("001");
        Assert.Equal(AvailabilityTrend.Stable, summary.Trend);
        Assert.Equal(7, summary.DeltaBikes);
        Assert.InRange(summary.WindowMinutes, 17, 19);
    }

    [Fact]
    public void GetTrendSummary_WhenWindowIsLessThanOneMinute_ReturnsStableWithZeroWindow()
    {
        var now = DateTime.UtcNow;
        var timestampsJson = $"[\"{now:o}\",\"{now.AddSeconds(30):o}\"]";
        var json = $$"""
            {
              "intervalMinutes": 1,
              "timestamps": {{timestampsJson}},
              "rows": [["001", 5, 7]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        Assert.Equal(new TrendSummary(AvailabilityTrend.Stable, 0, 0), service.GetTrendSummary("001"));
    }

    #endregion

    #region AppendLiveSnapshot

    [Fact]
    public void AppendLiveSnapshot_WhenNoExistingData_CreatesMinimalSeries()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("no data")));
        var service = new SnapshotService(httpClient, "https://aggregator.example");

        service.AppendLiveSnapshot(new Dictionary<string, int> { ["001"] = 7 });

        var counts = service.GetStationCounts("001");
        Assert.Single(counts);
        Assert.Equal(7, counts[0]);
    }

    [Fact]
    public void AppendLiveSnapshot_AppendsToExistingSeries()
    {
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z"],
              "rows": [["001", 5]]
            }
            """);

        service.AppendLiveSnapshot(new Dictionary<string, int> { ["001"] = 8 });

        var counts = service.GetStationCounts("001");
        Assert.Equal(2, counts.Length);
        Assert.Equal(5, counts[0]);
        Assert.Equal(8, counts[1]);
    }

    [Fact]
    public void AppendLiveSnapshot_AddsNewStations()
    {
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z"],
              "rows": [["001", 5]]
            }
            """);

        service.AppendLiveSnapshot(new Dictionary<string, int> { ["001"] = 8, ["002"] = 3 });

        Assert.Equal([5, 8], service.GetStationCounts("001"));
        Assert.Equal([0, 3], service.GetStationCounts("002"));
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
