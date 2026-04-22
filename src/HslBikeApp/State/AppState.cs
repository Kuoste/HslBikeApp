using HslBikeApp.Models;
using HslBikeApp.Services;
using Microsoft.JSInterop;

namespace HslBikeApp.State;

public class AppState : IAsyncDisposable
{
    private readonly StationService _stationService;
    private readonly StatisticsService _statisticsService;
    private readonly CycleLaneService _cycleLaneService;
    private readonly SnapshotService _snapshotService;
    private readonly LiveStationService _liveStationService;
    private readonly IJSRuntime _js;

    // --------------- scheduling ---------------
    private const int LivePollIntervalMs = 30_000;
    private const int SnapshotBufferSeconds = 50;
    private const int SnapshotRetrySeconds = 30;

    private Timer? _livePollTimer;
    private Timer? _snapshotRefreshTimer;
    private bool _tabVisible = true;
    private DotNetObjectReference<AppState>? _dotNetRef;

    // --------------- stations ---------------
    public List<BikeStation> Stations { get; private set; } = [];
    public bool IsLoadingStations { get; private set; }
    public string? StationError { get; private set; }
    public string? StationStatusMessage { get; private set; }
    public DateTime? LatestLiveDataRetrievedAtUtc { get; private set; }

    // --------------- live data ---------------
    public DateTime? LiveTimestamp => _liveStationService.LiveTimestamp;

    // --------------- search / filter ---------------
    public string SearchQuery { get; private set; } = "";
    public List<BikeStation> FilteredStations
    {
        get
        {
            if (string.IsNullOrEmpty(SearchQuery)) return Stations;
            var query = SearchQuery.ToLowerInvariant();
            return Stations.Where(station =>
                station.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                station.Address.Contains(query, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    // --------------- selected station ---------------
    public BikeStation? SelectedStation { get; private set; }

    // --------------- monthly statistics ---------------
    public MonthlyStationStatistics? Statistics { get; private set; }
    public List<DestinationRow> Destinations { get; private set; } = [];
    public bool IsLoadingStatistics { get; private set; }

    // --------------- cycle lanes ---------------
    public List<CycleLane> CycleLanes { get; private set; } = [];
    public bool ShowCycleLanes { get; private set; }
    public bool IsLoadingCycleLanes { get; private set; }

    // --------------- change tracking ---------------
    public Dictionary<string, int> BikeChanges { get; private set; } = new();

    public event Action? OnStateChanged;

    public AppState(
        StationService stationService,
        StatisticsService statisticsService,
        CycleLaneService cycleLaneService,
        SnapshotService snapshotService,
        LiveStationService liveStationService,
        IJSRuntime js)
    {
        _stationService = stationService;
        _statisticsService = statisticsService;
        _cycleLaneService = cycleLaneService;
        _snapshotService = snapshotService;
        _liveStationService = liveStationService;
        _js = js;
    }

    public async Task InitAsync()
    {
        await _snapshotService.FetchSnapshotsAsync();
        await RefreshLiveAsync();
        ScheduleNextSnapshotRefresh();
        StartLivePoll();

        _dotNetRef = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("VisibilityInterop.register", _dotNetRef);
    }

    public void SetSearchQuery(string query)
    {
        SearchQuery = query;
        NotifyStateChanged();
    }

    /// <summary>Refreshes live station data from <c>GET /api/stations</c>.</summary>
    public async Task RefreshLiveAsync()
    {
        IsLoadingStations = true;
        StationError = null;
        StationStatusMessage = null;
        NotifyStateChanged();

        try
        {
            var previous = Stations.ToDictionary(station => station.Id, station => station.BikesAvailable);
            var latestStations = await _liveStationService.RefreshAsync();

            if (latestStations.Count == 0 && _liveStationService.LiveTimestamp is null)
            {
                StationError = "Could not load live bike stations from the aggregator backend.";
                return;
            }

            Stations = [.. latestStations];
            LatestLiveDataRetrievedAtUtc = _liveStationService.LiveTimestamp;

            if (Stations.Count == 0)
            {
                StationStatusMessage = "No active HSL city bike stations are currently published. This usually means the bike season has not started yet or the upstream feed is temporarily empty.";
            }

            if (previous.Count > 0)
            {
                BikeChanges = new Dictionary<string, int>();
                foreach (var station in Stations)
                {
                    if (previous.TryGetValue(station.Id, out var previousCount) && previousCount != station.BikesAvailable)
                        BikeChanges[station.Id] = station.BikesAvailable - previousCount;
                }
            }
        }
        catch (Exception exception)
        {
            StationError = $"Could not load live bike stations from the aggregator backend. {exception.Message}";
        }
        finally
        {
            IsLoadingStations = false;
            NotifyStateChanged();
        }
    }

    /// <summary>Fetches a fresh snapshot batch and reschedules the next fetch.</summary>
    private async Task RefreshSnapshotsAsync()
    {
        var previousTimestamp = _snapshotService.LatestSnapshotTimestamp;
        await _snapshotService.FetchSnapshotsAsync();
        var newTimestamp = _snapshotService.LatestSnapshotTimestamp;

        // If the server returned the same timestamp, schedule a retry after a short delay
        // rather than busy-polling at the full interval.
        if (newTimestamp == previousTimestamp)
        {
            ScheduleSnapshotRefreshIn(TimeSpan.FromSeconds(SnapshotRetrySeconds));
        }
        else
        {
            ScheduleNextSnapshotRefresh();
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Schedules the next snapshot refetch based on <c>LatestSnapshotTimestamp + IntervalMinutes + buffer</c>.
    /// Falls back to one interval from now if no timestamp is available.
    /// </summary>
    internal void ScheduleNextSnapshotRefresh()
    {
        var nextAt = _snapshotService.ComputeNextRefreshAt(SnapshotBufferSeconds);
        var delay = nextAt - DateTime.UtcNow;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        ScheduleSnapshotRefreshIn(delay);
    }

    private void ScheduleSnapshotRefreshIn(TimeSpan delay)
    {
        _snapshotRefreshTimer?.Dispose();
        _snapshotRefreshTimer = new Timer(
            async _ => await RefreshSnapshotsAsync(),
            null,
            (long)Math.Max(0, delay.TotalMilliseconds),
            Timeout.Infinite);
    }

    private void StartLivePoll()
    {
        _livePollTimer?.Dispose();
        _livePollTimer = new Timer(
            async _ =>
            {
                if (!_tabVisible) return;
                await RefreshLiveAsync();
            },
            null,
            LivePollIntervalMs,
            LivePollIntervalMs);
    }

    /// <summary>
    /// Called by JS when the tab visibility changes.
    /// On wake, triggers an immediate live refresh and a snapshot refresh if overdue.
    /// </summary>
    [JSInvokable]
    public async Task OnVisibilityChanged(bool isVisible)
    {
        _tabVisible = isVisible;
        if (!isVisible) return;

        await RefreshLiveAsync();

        var tLast = _snapshotService.LatestSnapshotTimestamp;
        if (tLast.HasValue && DateTime.UtcNow >= tLast.Value.AddMinutes(_snapshotService.IntervalMinutes))
        {
            await RefreshSnapshotsAsync();
        }
    }

    public async Task SelectStationAsync(BikeStation station)
    {
        ArgumentNullException.ThrowIfNull(station);

        SelectedStation = station;
        Destinations = [];
        Statistics = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Lazy-loads monthly statistics for the currently selected station.
    /// Called when the user opens the monthly stats view.
    /// </summary>
    public async Task LoadStatisticsAsync()
    {
        if (SelectedStation is null) return;

        IsLoadingStatistics = true;
        NotifyStateChanged();

        try
        {
            Statistics = await _statisticsService.FetchStatisticsAsync(SelectedStation.Id);
            Destinations = Statistics?.Destinations.ParseRows() ?? [];
        }
        catch
        {
            Statistics = null;
            Destinations = [];
        }
        finally
        {
            IsLoadingStatistics = false;
            NotifyStateChanged();
        }
    }

    public void ClearSelection()
    {
        SelectedStation = null;
        Destinations = [];
        Statistics = null;
        IsLoadingStatistics = false;
        NotifyStateChanged();
    }

    public async Task ToggleCycleLanesAsync()
    {
        ShowCycleLanes = !ShowCycleLanes;
        NotifyStateChanged();

        if (ShowCycleLanes && CycleLanes.Count == 0)
        {
            IsLoadingCycleLanes = true;
            NotifyStateChanged();
            try
            {
                CycleLanes = await _cycleLaneService.FetchCycleLanesAsync();
            }
            catch
            {
                CycleLanes = [];
            }
            finally
            {
                IsLoadingCycleLanes = false;
                NotifyStateChanged();
            }
        }
    }

    public async Task RefreshNowAsync()
    {
        await RefreshLiveAsync();
    }

    /// <summary>Derives a trend using snapshot data + current live state for a given station.</summary>
    public AvailabilityTrend GetTrend(string stationId) =>
        _snapshotService.GetTrend(
            stationId,
            _liveStationService.GetLiveCount(stationId),
            _liveStationService.LiveTimestamp);

    /// <summary>Returns a trend summary using snapshot data + current live state for a given station.</summary>
    public TrendSummary GetTrendSummary(string stationId) =>
        _snapshotService.GetTrendSummary(
            stationId,
            _liveStationService.GetLiveCount(stationId),
            _liveStationService.LiveTimestamp);

    /// <summary>Returns the snapshot timestamps.</summary>
    public IReadOnlyList<DateTime> SnapshotTimestamps => _snapshotService.Timestamps;

    /// <summary>Returns the snapshot interval in minutes.</summary>
    public int SnapshotIntervalMinutes => _snapshotService.IntervalMinutes;

    /// <summary>Returns bike counts for a station from the snapshot time-series.</summary>
    public int[] GetStationSnapshotCounts(string stationId) =>
        _snapshotService.GetStationCounts(stationId);

    /// <summary>Returns whether there is a polling gap at the given snapshot index.</summary>
    public bool IsSnapshotGap(int index) => _snapshotService.IsGap(index);

    /// <summary>Returns the live count for a station, or <c>null</c> if unavailable.</summary>
    public int? GetLiveCount(string stationId) =>
        _liveStationService.GetLiveCount(stationId);

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        _livePollTimer?.Dispose();
        _snapshotRefreshTimer?.Dispose();

        if (_dotNetRef is not null)
        {
            try { await _js.InvokeVoidAsync("VisibilityInterop.dispose"); } catch { /* ignore on dispose */ }
            _dotNetRef.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

