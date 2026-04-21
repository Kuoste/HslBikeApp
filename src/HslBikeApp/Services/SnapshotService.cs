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

    /// <summary>
    /// Derives an <see cref="AvailabilityTrend"/> for a station from recent snapshot data.
    /// Uses the last 6 data points (or all if fewer).
    /// </summary>
    public AvailabilityTrend GetTrend(string stationId) =>
        GetTrendSummary(stationId).Trend;

    /// <summary>
    /// Returns a trend together with the signed bike delta and analysed time window.
    /// Uses the last 6 data points (or all if fewer).
    /// </summary>
    public TrendSummary GetTrendSummary(string stationId)
    {
        var counts = GetStationCounts(stationId);
        var timestamps = Timestamps;
        if (counts.Length < 2 || timestamps.Count < 2)
            return new TrendSummary(AvailabilityTrend.Stable, 0, 0);

        var len = Math.Min(counts.Length, timestamps.Count);
        var windowSize = Math.Min(6, len);
        var startIndex = len - windowSize;

        var firstCount = counts[startIndex];
        var lastCount = counts[len - 1];
        var timeDifference = timestamps[len - 1] - timestamps[startIndex];

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

    /// <summary>
    /// Returns the last <paramref name="count"/> bike counts for a station (sparkline data).
    /// </summary>
    public List<int> GetSparkline(string stationId, int count = 12)
    {
        var counts = GetStationCounts(stationId);
        if (counts.Length == 0) return [];

        var start = Math.Max(0, counts.Length - count);
        return counts[start..].ToList();
    }

    /// <summary>
    /// Appends a live data point from <c>GET /api/stations</c> into the in-memory series.
    /// </summary>
    public void AppendLiveSnapshot(Dictionary<string, int> bikeCounts)
    {
        if (_timeSeries is null)
        {
            // Create a minimal time-series with just this one data point
            var timestamps = new List<DateTime> { DateTime.UtcNow };
            var rows = bikeCounts.Select(kvp =>
                (IReadOnlyList<object?>)[kvp.Key, kvp.Value]).ToList();

            _timeSeries = new SnapshotTimeSeries
            {
                IntervalMinutes = 15,
                Timestamps = timestamps,
                RawRows = rows
            };
            _stationSeries = _timeSeries.ParseRows();
            return;
        }

        // Append timestamp
        var newTimestamps = _timeSeries.Timestamps.ToList();
        newTimestamps.Add(DateTime.UtcNow);

        // Append count to existing series, and create new series for new stations
        var updatedSeries = new List<StationCountSeries>();
        var processedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var series in _stationSeries)
        {
            var newCount = bikeCounts.TryGetValue(series.StationId, out var c) ? c : 0;
            var newCounts = new int[series.Counts.Length + 1];
            series.Counts.CopyTo(newCounts, 0);
            newCounts[^1] = newCount;
            updatedSeries.Add(new StationCountSeries { StationId = series.StationId, Counts = newCounts });
            processedIds.Add(series.StationId);
        }

        foreach (var kvp in bikeCounts)
        {
            if (processedIds.Contains(kvp.Key)) continue;
            var counts = new int[newTimestamps.Count];
            counts[^1] = kvp.Value;
            updatedSeries.Add(new StationCountSeries { StationId = kvp.Key, Counts = counts });
        }

        // Cap at 60 data points
        const int maxPoints = 60;
        if (newTimestamps.Count > maxPoints)
        {
            var skip = newTimestamps.Count - maxPoints;
            newTimestamps = newTimestamps.Skip(skip).ToList();
            updatedSeries = updatedSeries.Select(s => new StationCountSeries
            {
                StationId = s.StationId,
                Counts = s.Counts.Skip(skip).ToArray()
            }).ToList();
        }

        _timeSeries = _timeSeries with { Timestamps = newTimestamps };
        _stationSeries = updatedSeries;
    }
}
