using HslBikeApp.Models;
using HslBikeApp.Services;

namespace HslBikeApp.State;

public class AppState : IDisposable
{
    private readonly StationService _stationService;
    private readonly AvailabilityService _availabilityService;
    private readonly HistoryService _historyService;
    private readonly CycleLaneService _cycleLaneService;
    private readonly SnapshotService _snapshotService;

    private Timer? _refreshTimer;
    private const int RefreshIntervalMs = 30_000;
    private const int MaxSnapshots = 60;

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
            var q = SearchQuery.ToLowerInvariant();
            return Stations.Where(s =>
                s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.Address.Contains(q, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    // --------------- selected station ---------------
    public BikeStation? SelectedStation { get; private set; }
    public List<StationHistory> History { get; private set; } = [];
    public bool IsLoadingHistory { get; private set; }
    public List<HourlyAvailability> AvailabilityProfile { get; private set; } = [];
    public bool IsLoadingAvailability { get; private set; }

    // --------------- cycle lanes ---------------
    public List<CycleLane> CycleLanes { get; private set; } = [];
    public bool ShowCycleLanes { get; private set; }
    public bool IsLoadingCycleLanes { get; private set; }

    // --------------- change tracking ---------------
    public Dictionary<string, int> BikeChanges { get; private set; } = new();

    // --------------- trend tracking ---------------
    private List<StationSnapshot> _snapshots = [];
    public IReadOnlyList<StationSnapshot> Snapshots => _snapshots;

    public event Action? OnStateChanged;

    public AppState(
        StationService stationService,
        AvailabilityService availabilityService,
        HistoryService historyService,
        CycleLaneService cycleLaneService,
        SnapshotService snapshotService)
    {
        _stationService = stationService;
        _availabilityService = availabilityService;
        _historyService = historyService;
        _cycleLaneService = cycleLaneService;
        _snapshotService = snapshotService;
    }

    public async Task InitAsync()
    {
        // Load pre-built snapshots first for instant trends
        _snapshots = await _snapshotService.FetchSnapshotsAsync();
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
            var previous = Stations.ToDictionary(s => s.Id, s => s.BikesAvailable);
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
                foreach (var s in Stations)
                {
                    if (previous.TryGetValue(s.Id, out var prev) && prev != s.BikesAvailable)
                        BikeChanges[s.Id] = s.BikesAvailable - prev;
                }
            }

            // Append live snapshot for trend tracking
            var snapshot = new StationSnapshot
            {
                Timestamp = DateTime.UtcNow,
                BikeCounts = Stations.ToDictionary(s => s.Id, s => s.BikesAvailable)
            };
            _snapshots.Add(snapshot);
            if (_snapshots.Count > MaxSnapshots)
                _snapshots = _snapshots.Skip(_snapshots.Count - MaxSnapshots).ToList();
        }
        catch (Exception ex)
        {
            StationError = $"Could not load live bike stations from the aggregator backend. {ex.Message}";
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
        History = [];
        AvailabilityProfile = [];
        IsLoadingHistory = true;
        IsLoadingAvailability = true;
        NotifyStateChanged();

        var historyTask = TryFetchHistoryAsync(station.Id);
        var availabilityTask = TryFetchAvailabilityAsync(station.Id);

        try
        {
            var history = await historyTask;
            var availability = await availabilityTask;

            if (SelectedStation?.Id == station.Id)
            {
                History = history;
                AvailabilityProfile = availability;
            }
        }
        finally
        {
            IsLoadingHistory = false;
            IsLoadingAvailability = false;
            NotifyStateChanged();
        }
    }

    private async Task<List<StationHistory>> TryFetchHistoryAsync(string stationId)
    {
        try
        {
            return await _historyService.FetchHistoryAsync(stationId);
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<HourlyAvailability>> TryFetchAvailabilityAsync(string stationId)
    {
        try
        {
            return await _availabilityService.FetchAvailabilityAsync(stationId);
        }
        catch
        {
            return [];
        }
    }

    public void ClearSelection()
    {
        SelectedStation = null;
        History = [];
        AvailabilityProfile = [];
        IsLoadingHistory = false;
        IsLoadingAvailability = false;
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

    public AvailabilityTrend GetTrend(string stationId)
    {
        if (_snapshots.Count < 2) return AvailabilityTrend.Stable;

        // Use last 6 snapshots (or all if fewer) to compute rate of change
        var recent = _snapshots.Skip(Math.Max(0, _snapshots.Count - 6)).ToList();
        var first = recent.First();
        var last = recent.Last();

        if (!first.BikeCounts.TryGetValue(stationId, out var firstCount) ||
            !last.BikeCounts.TryGetValue(stationId, out var lastCount))
            return AvailabilityTrend.Stable;

        var timeDiffMinutes = (last.Timestamp - first.Timestamp).TotalMinutes;
        if (timeDiffMinutes < 1) return AvailabilityTrend.Stable;

        var ratePerMinute = (lastCount - firstCount) / timeDiffMinutes;

        return ratePerMinute switch
        {
            <= -2 => AvailabilityTrend.RapidDecrease,
            <= -0.5 => AvailabilityTrend.Decreasing,
            >= 2 => AvailabilityTrend.RapidIncrease,
            >= 0.5 => AvailabilityTrend.Increasing,
            _ => AvailabilityTrend.Stable
        };
    }

    public List<int> GetSparkline(string stationId, int count = 12)
    {
        return _snapshots
            .Skip(Math.Max(0, _snapshots.Count - count))
            .Select(s => s.BikeCounts.TryGetValue(stationId, out var c) ? c : 0)
            .ToList();
    }

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
