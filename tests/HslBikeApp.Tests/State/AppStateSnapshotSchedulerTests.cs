using System.Net;
using System.Text;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.State;

/// <summary>
/// Tests for the snapshot-refresh scheduling calculation exposed by <see cref="SnapshotService.ComputeNextRefreshAt"/>.
/// AppState uses this value to schedule its snapshot timer.
/// </summary>
public class AppStateSnapshotSchedulerTests
{
    private static SnapshotService CreateServiceWithFetch(string json)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })));
        var service = new SnapshotService(httpClient, "https://aggregator.example");
        service.FetchSnapshotsAsync().GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public void ComputeNextRefreshAt_WhenSnapshotLoaded_ReturnsLastTimestampPlusIntervalPlusBuffer()
    {
        // Interval = 15 min, last timestamp = 10:00 UTC, buffer = 50 s
        // Expected next = 10:15:50 UTC
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z"],
              "rows": [["001", 5]]
            }
            """);

        var nextRefresh = service.ComputeNextRefreshAt(bufferSeconds: 50);

        var expected = DateTime.Parse("2026-06-01T10:15:50Z").ToUniversalTime();
        Assert.Equal(expected, nextRefresh);
    }

    [Fact]
    public void ComputeNextRefreshAt_WhenNoSnapshotsLoaded_ReturnsFallbackInFuture()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("no data")));
        var service = new SnapshotService(httpClient, "https://aggregator.example");

        var before = DateTime.UtcNow;
        var nextRefresh = service.ComputeNextRefreshAt(bufferSeconds: 50);
        var after = DateTime.UtcNow;

        // Falls back to UtcNow + IntervalMinutes (default 15 min)
        Assert.True(nextRefresh >= before.AddMinutes(14));
        Assert.True(nextRefresh <= after.AddMinutes(16));
    }

    [Fact]
    public void ComputeNextRefreshAt_WithCustomIntervalMinutes_UsesServerProvidedInterval()
    {
        // Server returns 30-minute interval
        var tLast = DateTime.Parse("2026-06-01T08:00:00Z").ToUniversalTime();
        var service = CreateServiceWithFetch(
            $$"""
            {
              "intervalMinutes": 30,
              "timestamps": ["{{tLast:o}}"],
              "rows": [["001", 3]]
            }
            """);

        var nextRefresh = service.ComputeNextRefreshAt(bufferSeconds: 60);

        var expected = tLast.AddMinutes(30).AddSeconds(60);
        Assert.Equal(expected, nextRefresh);
    }

    [Fact]
    public void ComputeNextRefreshAt_WhenLastTimestampIsInPast_ReturnsTimeInPast_CallerSchedulesImmediately()
    {
        // If the server's last snapshot was 20 min ago with 15-min interval + 50s buffer,
        // the computed next-at is 5 min 50 s in the past → caller should schedule delay=0.
        var tLast = DateTime.UtcNow.AddMinutes(-20);
        var service = CreateServiceWithFetch(
            $$"""
            {
              "intervalMinutes": 15,
              "timestamps": ["{{tLast:o}}"],
              "rows": [["001", 5]]
            }
            """);

        var nextRefresh = service.ComputeNextRefreshAt(bufferSeconds: 50);

        // nextRefresh = tLast + 15 min + 50 s ≈ 4 min 10 s in the past
        Assert.True(nextRefresh < DateTime.UtcNow,
            "When overdue, ComputeNextRefreshAt returns a past time so the caller can schedule immediately.");
    }

    [Fact]
    public void ComputeNextRefreshAt_CalledTwiceWithSameTimestamp_ReturnsSameValue()
    {
        // Simulates stale-response scenario: server returns same T_last twice.
        // AppState should detect unchanged timestamp and reschedule with a shorter retry delay,
        // not by calling ComputeNextRefreshAt (which would return the same far-future time).
        // This test verifies the method itself is deterministic for the same data.
        var service = CreateServiceWithFetch(
            """
            {
              "intervalMinutes": 15,
              "timestamps": ["2026-06-01T10:00:00Z"],
              "rows": [["001", 5]]
            }
            """);

        var first = service.ComputeNextRefreshAt(bufferSeconds: 50);
        var second = service.ComputeNextRefreshAt(bufferSeconds: 50);

        Assert.Equal(first, second);
    }
}
