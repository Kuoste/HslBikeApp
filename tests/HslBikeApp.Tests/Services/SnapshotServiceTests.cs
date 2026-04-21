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
        // Rate must be <= -2 bikes/min for RapidDecrease
        // 6 points at 1-min intervals: 20 -> 0 over 5 min = -4 bikes/min
        var timestamps = Enumerable.Range(0, 6)
            .Select(i => DateTime.UtcNow.AddMinutes(i).ToString("o"));
        var timestampsJson = "[" + string.Join(",", timestamps.Select(t => $"\"{t}\"")) + "]";
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
            .Select(i => DateTime.UtcNow.AddMinutes(i * 5).ToString("o"));
        var timestampsJson = "[" + string.Join(",", timestamps.Select(t => $"\"{t}\"")) + "]";
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
    public void GetTrendSummary_UsesLastSixPointsForWindowAndDelta()
    {
        var timestamps = Enumerable.Range(0, 8)
            .Select(i => DateTime.UtcNow.AddMinutes(i).ToString("o"));
        var timestampsJson = "[" + string.Join(",", timestamps.Select(t => $"\"{t}\"")) + "]";
        var json = $$"""
            {
              "intervalMinutes": 1,
              "timestamps": {{timestampsJson}},
              "rows": [["001", 99, 99, 0, 1, 2, 3, 4, 5]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        Assert.Equal(new TrendSummary(AvailabilityTrend.Increasing, 5, 5), service.GetTrendSummary("001"));
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

    #region GetSparkline

    [Fact]
    public void GetSparkline_ReturnsLastNCounts()
    {
        var timestamps = Enumerable.Range(0, 15)
            .Select(i => DateTime.UtcNow.AddMinutes(i * 5).ToString("o"));
        var timestampsJson = "[" + string.Join(",", timestamps.Select(t => $"\"{t}\"")) + "]";
        var counts = string.Join(",", Enumerable.Range(0, 15));
        var json = $$"""
            {
              "intervalMinutes": 5,
              "timestamps": {{timestampsJson}},
              "rows": [["001", {{counts}}]]
            }
            """;
        var service = CreateServiceWithFetch(json);

        var sparkline = service.GetSparkline("001", 5);

        Assert.Equal(5, sparkline.Count);
        Assert.Equal([10, 11, 12, 13, 14], sparkline);
    }

    [Fact]
    public void GetSparkline_WhenNoData_ReturnsEmpty()
    {
        var service = CreateServiceWithFetch(
            """{"intervalMinutes":15,"timestamps":[],"rows":[]}""");

        Assert.Empty(service.GetSparkline("001"));
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
