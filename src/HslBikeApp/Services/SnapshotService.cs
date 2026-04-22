using System.Net.Http.Json;
using HslBikeApp.Models;

namespace HslBikeApp.Services;

public class SnapshotService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    /// Parsed per-station series, populated after <see cref="FetchSnapshotsAsync"/>.
    private IReadOnlyList<StationCountSeries> _stationSeries = [];

    /// Raw time-series metadata, populated after <see cref="FetchSnapshotsAsync"/>.
    private SnapshotTimeSeries? _timeSeries;

    public SnapshotService(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Fetches the columnar snapshot time-series from the aggregator.
    /// Returns the parsed <see cref="SnapshotTimeSeries"/> or <c>null</c> on failure.
    /// </summary>
    public async Task<SnapshotTimeSeries?> FetchSnapshotsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/snapshots");
            response.EnsureSuccessStatusCode();
            var timeSeries = await response.Content.ReadFromJsonAsync<SnapshotTimeSeries>();
            if (timeSeries is null) return null;

            _timeSeries = timeSeries;
            _stationSeries = timeSeries.ParseRows();
            return timeSeries;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Returns the bike count array for a single station, or empty if not found.</summary>
    public int[] GetStationCounts(string stationId)
    {
        var series = _stationSeries.FirstOrDefault(s =>
            s.StationId.Equals(stationId, StringComparison.OrdinalIgnoreCase));
        return series?.Counts ?? [];
    }

    /// <summary>Returns the timestamps from the last successful fetch, or empty.</summary>
    public IReadOnlyList<DateTime> Timestamps => _timeSeries?.Timestamps ?? [];

    /// <summary>Returns the interval in minutes, or 15 as the default.</summary>
    public int IntervalMinutes => _timeSeries?.IntervalMinutes ?? 15;

    /// <summary>Determines if there is a polling gap at the given index.</summary>
    public bool IsGap(int index) => _timeSeries?.IsGap(index) ?? false;

    /// <summary>Returns the timestamp of the latest snapshot, or <c>null</c> if none loaded.</summary>
    public DateTime? LatestSnapshotTimestamp => _timeSeries?.Timestamps.LastOrDefault();

    /// <summary>
    /// Computes the UTC time at which the next snapshot refetch should be scheduled:
    /// <c>LatestSnapshotTimestamp + IntervalMinutes + bufferSeconds</c>.
    /// Falls back to <c>UtcNow + IntervalMinutes</c> when no snapshot has been loaded.
    /// </summary>
    public DateTime ComputeNextRefreshAt(int bufferSeconds = 50)
    {
        var tLast = LatestSnapshotTimestamp;
        return tLast.HasValue
            ? tLast.Value.AddMinutes(IntervalMinutes).AddSeconds(bufferSeconds)
            : DateTime.UtcNow.AddMinutes(IntervalMinutes);
    }

    /// <summary>Derives an <see cref="AvailabilityTrend"/> for a station from snapshot + live data.</summary>
    public AvailabilityTrend GetTrend(string stationId, int? liveCount = null, DateTime? liveTimestamp = null) =>
        GetTrendSummary(stationId, liveCount, liveTimestamp).Trend;

    /// <summary>
    /// Returns a trend together with the signed bike delta and analysed time window.
    /// <para>
    /// Trend logic (all in UTC):
    /// <list type="bullet">
    ///   <item>No live data → compare last two snapshots (legacy fallback).</item>
    ///   <item>Fewer than 2 snapshots → <c>Stable, 0, 0</c>.</item>
    ///   <item>1 snapshot + live, gap &lt; 1 min → <c>Stable, 0, 0</c>.</item>
    ///   <item>live within 10 min of last snapshot → use second-last snapshot vs live.</item>
    ///   <item>Otherwise → use last snapshot vs live.</item>
    /// </list>
    /// </para>
    /// </summary>
    public TrendSummary GetTrendSummary(string stationId, int? liveCount = null, DateTime? liveTimestamp = null)
    {
        var counts = GetStationCounts(stationId);
        var timestamps = Timestamps;

        // Need at least one snapshot for any calculation
        if (counts.Length == 0 || timestamps.Count == 0)
            return new TrendSummary(AvailabilityTrend.Stable, 0, 0);

        // No live data — fall back to comparing the last two snapshots
        if (liveCount is null || liveTimestamp is null)
        {
            if (counts.Length < 2 || timestamps.Count < 2)
                return new TrendSummary(AvailabilityTrend.Stable, 0, 0);

            return ComputeTrend(counts[^2], timestamps[^2], counts[^1], timestamps[^1]);
        }

        // We have live data
        var tLast = timestamps[^1];
        var sLast = counts[^1];

        // Only one snapshot available
        if (counts.Length == 1 || timestamps.Count == 1)
            return ComputeTrend(sLast, tLast, liveCount.Value, liveTimestamp.Value);

        var tPrev = timestamps[^2];
        var sPrev = counts[^2];

        // If live timestamp is within 10 minutes of the last snapshot, use the previous snapshot
        // as the baseline to get a more meaningful window.
        const int tooCloseThresholdMinutes = 10;
        var startCount = (liveTimestamp.Value - tLast).TotalMinutes < tooCloseThresholdMinutes
            ? sPrev
            : sLast;
        var startTimestamp = (liveTimestamp.Value - tLast).TotalMinutes < tooCloseThresholdMinutes
            ? tPrev
            : tLast;

        return ComputeTrend(startCount, startTimestamp, liveCount.Value, liveTimestamp.Value);
    }

    private static TrendSummary ComputeTrend(int firstCount, DateTime firstTime, int lastCount, DateTime lastTime)
    {
        var timeDifference = lastTime - firstTime;
        if (timeDifference.TotalMinutes < 1)
            return new TrendSummary(AvailabilityTrend.Stable, 0, 0);

        var deltaBikes = lastCount - firstCount;
        var windowMinutes = Math.Max(1, (int)Math.Round(timeDifference.TotalMinutes, MidpointRounding.AwayFromZero));
        var ratePerMinute = deltaBikes / timeDifference.TotalMinutes;

        var trend = ratePerMinute switch
        {
            <= -2 => AvailabilityTrend.RapidDecrease,
            <= -0.5 => AvailabilityTrend.Decreasing,
            >= 2 => AvailabilityTrend.RapidIncrease,
            >= 0.5 => AvailabilityTrend.Increasing,
            _ => AvailabilityTrend.Stable
        };

        return new TrendSummary(trend, deltaBikes, windowMinutes);
    }
}
