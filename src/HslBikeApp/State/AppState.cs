using HslBikeApp.Models;
using HslBikeApp.Services;

namespace HslBikeApp.State;

public class AppState : IDisposable
{
    private readonly StationService _stationService;
    private readonly StatisticsService _statisticsService;
    private readonly CycleLaneService _cycleLaneService;
    private readonly SnapshotService _snapshotService;

    private Timer? _refreshTimer;
    private const int RefreshIntervalMs = 30_000;

    // --------------- stations ---------------
    public List<BikeStation> Stations { get; private set; } = [];
    public bool IsLoadingStations { get; private set; }
    public string? StationError { get; private set; }
    public string? StationStatusMessage { get; private set; }
    public DateTime? LatestLiveDataRetrievedAtUtc { get; private set; }

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
        SnapshotService snapshotService)
    {
        _stationService = stationService;
        _statisticsService = statisticsService;
        _cycleLaneService = cycleLaneService;
        _snapshotService = snapshotService;
    }

    public async Task InitAsync()
    {
        await _snapshotService.FetchSnapshotsAsync();
        await LoadStationsAsync();
        StartAutoRefresh();
    }

    public void SetSearchQuery(string query)
    {
        SearchQuery = query;
        NotifyStateChanged();
    }

    public async Task LoadStationsAsync()
    {
        IsLoadingStations = true;
        StationError = null;
        StationStatusMessage = null;
        NotifyStateChanged();

        try
        {
            var previous = Stations.ToDictionary(station => station.Id, station => station.BikesAvailable);
            var latestStations = await _stationService.FetchStationsAsync();

            if (!_stationService.LastFetchSucceeded)
            {
                StationError = _stationService.LastErrorMessage;
                return;
            }

            Stations = latestStations;
            LatestLiveDataRetrievedAtUtc = DateTime.UtcNow;

            if (Stations.Count == 0)
            {
                StationStatusMessage = "No active HSL city bike stations are currently published. This usually means the bike season has not started yet or the upstream feed is temporarily empty.";
            }

            // Compute bike count deltas
            if (previous.Count > 0)
            {
                BikeChanges = new Dictionary<string, int>();
                foreach (var station in Stations)
                {
                    if (previous.TryGetValue(station.Id, out var previousCount) && previousCount != station.BikesAvailable)
                        BikeChanges[station.Id] = station.BikesAvailable - previousCount;
                }
            }

            // Append live snapshot for trend tracking
            _snapshotService.AppendLiveSnapshot(
                Stations.ToDictionary(station => station.Id, station => station.BikesAvailable));
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
        await LoadStationsAsync();
        RestartAutoRefresh();
    }

    /// <summary>Derives a trend from the snapshot service for a given station.</summary>
    public AvailabilityTrend GetTrend(string stationId) =>
        _snapshotService.GetTrend(stationId);

    /// <summary>Returns a trend summary from the snapshot service for a given station.</summary>
    public TrendSummary GetTrendSummary(string stationId) =>
        _snapshotService.GetTrendSummary(stationId);

    /// <summary>Returns the snapshot timestamps.</summary>
    public IReadOnlyList<DateTime> SnapshotTimestamps => _snapshotService.Timestamps;

    /// <summary>Returns the snapshot interval in minutes.</summary>
    public int SnapshotIntervalMinutes => _snapshotService.IntervalMinutes;

    /// <summary>Returns bike counts for a station from the snapshot time-series.</summary>
    public int[] GetStationSnapshotCounts(string stationId) =>
        _snapshotService.GetStationCounts(stationId);

    /// <summary>Returns whether there is a polling gap at the given snapshot index.</summary>
    public bool IsSnapshotGap(int index) => _snapshotService.IsGap(index);

    private void StartAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = new Timer(async _ => await LoadStationsAsync(), null, RefreshIntervalMs, RefreshIntervalMs);
    }

    private void RestartAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = new Timer(async _ => await LoadStationsAsync(), null, RefreshIntervalMs, RefreshIntervalMs);
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    public void Dispose()
    {
        _refreshTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
