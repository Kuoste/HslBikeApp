using HslBikeApp.Models;
using HslBikeApp.Services;
using HslBikeApp.State;
using System.Net;
using System.Text.Json;

namespace HslBikeApp.Tests.State;

public class AppStateTests
{
    private static AppState CreateAppState(List<BikeStation>? stations = null)
    {
        var stationHandler = new MockHttpHandler();
        if (stations is not null)
        {
            var stationJson = JsonSerializer.Serialize(stations, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            stationHandler.SetResponse("/api/stations", stationJson);
        }

        var stationService = new StationService(
            new HttpClient(stationHandler) { BaseAddress = new Uri("https://test.local/") },
            "https://test.local");
        var statisticsService = new StatisticsService(new HttpClient(new MockHttpHandler()), "https://test.local");
        var cycleLaneService = new CycleLaneService(new HttpClient(new MockHttpHandler()));
        var snapshotService = new SnapshotService(
            new HttpClient(new MockHttpHandler()) { BaseAddress = new Uri("https://test.local/") },
            "https://test.local");

        return new AppState(stationService, statisticsService, cycleLaneService, snapshotService);
    }

    [Fact]
    public void SetSearchQuery_FiltersStations()
    {
        var state = CreateAppState();
        typeof(AppState).GetProperty("Stations")!.SetValue(state, new List<BikeStation>
        {
            new() { Id = "1", Name = "Kaivopuisto", Address = "Kaivopuisto 1" },
            new() { Id = "2", Name = "Kamppi", Address = "Kamppi 2" },
            new() { Id = "3", Name = "Kallio", Address = "Kallio 3" }
        });

        state.SetSearchQuery("kamp");
        Assert.Single(state.FilteredStations);
        Assert.Equal("Kamppi", state.FilteredStations[0].Name);
    }

    [Fact]
    public void SetSearchQuery_CaseInsensitive()
    {
        var state = CreateAppState();
        typeof(AppState).GetProperty("Stations")!.SetValue(state, new List<BikeStation>
        {
            new() { Id = "1", Name = "Kaivopuisto", Address = "addr" },
            new() { Id = "2", Name = "KAMPPI", Address = "addr" }
        });

        state.SetSearchQuery("kamppi");
        Assert.Single(state.FilteredStations);
    }

    [Fact]
    public void SetSearchQuery_Empty_ReturnsAll()
    {
        var state = CreateAppState();
        typeof(AppState).GetProperty("Stations")!.SetValue(state, new List<BikeStation>
        {
            new() { Id = "1", Name = "A", Address = "" },
            new() { Id = "2", Name = "B", Address = "" }
        });

        state.SetSearchQuery("");
        Assert.Equal(2, state.FilteredStations.Count);
    }

    [Fact]
    public void GetTrend_WhenNoSnapshots_ReturnsStable()
    {
        var state = CreateAppState();
        Assert.Equal(AvailabilityTrend.Stable, state.GetTrend("001"));
    }

    [Fact]
    public void GetTrendSummary_WhenNoSnapshots_ReturnsStableWithZeroDeltaAndWindow()
    {
        var state = CreateAppState();

        Assert.Equal(new TrendSummary(AvailabilityTrend.Stable, 0, 0), state.GetTrendSummary("001"));
    }

    [Fact]
    public void ClearSelection_ResetsState()
    {
        var state = CreateAppState();
        state.ClearSelection();

        Assert.Null(state.SelectedStation);
        Assert.Empty(state.Destinations);
        Assert.Null(state.Statistics);
        Assert.False(state.IsLoadingStatistics);
    }

    [Fact]
    public async Task SelectStationAsync_SetsSelectedStation()
    {
        var state = CreateAppState();
        var station = new BikeStation { Id = "001", Name = "Kamppi", Address = "Address" };

        await state.SelectStationAsync(station);

        Assert.Equal(station, state.SelectedStation);
        Assert.Empty(state.Destinations);
        Assert.Null(state.Statistics);
    }

    [Fact]
    public void OnStateChanged_IsRaised()
    {
        var state = CreateAppState();
        var raised = false;
        state.OnStateChanged += () => raised = true;

        state.SetSearchQuery("test");
        Assert.True(raised);
    }

    [Fact]
    public async Task LoadStationsAsync_PopulatesStations_FromAggregatorResponse()
    {
        var stations = new List<BikeStation>
        {
            new()
            {
                Id = "001",
                Name = "Kaivopuisto",
                Latitude = 60.15,
                Longitude = 24.95,
                Capacity = 20,
                BikesAvailable = 7,
                SpacesAvailable = 13,
                IsActive = true
            }
        };

        var state = CreateAppState(stations: stations);

        await state.LoadStationsAsync();

        Assert.Single(state.Stations);
        Assert.Null(state.StationError);
        Assert.Null(state.StationStatusMessage);
        Assert.Equal("Kaivopuisto", state.Stations[0].Name);
    }

    [Fact]
    public async Task LoadStationsAsync_EmptyFeed_SetsStatusMessage()
    {
        var state = CreateAppState(stations: []);

        await state.LoadStationsAsync();

        Assert.Empty(state.Stations);
        Assert.Null(state.StationError);
        Assert.NotNull(state.StationStatusMessage);
    }

    [Fact]
    public async Task LoadStationsAsync_WhenStationEndpointFails_SetsStationError()
    {
        var state = CreateAppState();

        await state.LoadStationsAsync();

        Assert.NotNull(state.StationError);
        Assert.Null(state.StationStatusMessage);
        Assert.Null(state.LatestLiveDataRetrievedAtUtc);
    }

    [Fact]
    public async Task LoadStationsAsync_WhenStationEndpointFails_PreservesExistingStations()
    {
        var state = CreateAppState();
        var existingStations = new List<BikeStation>
        {
            new() { Id = "001", Name = "Kaivopuisto", BikesAvailable = 7 }
        };
        typeof(AppState).GetProperty("Stations")!.SetValue(state, existingStations);

        await state.LoadStationsAsync();

        Assert.Single(state.Stations);
        Assert.Equal("Kaivopuisto", state.Stations[0].Name);
    }

    /// Simple HttpMessageHandler mock for tests
    private class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = new();

        public void SetResponse(string urlContains, string json)
        {
            _responses[urlContains] = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            foreach (var kvp in _responses)
            {
                if (url.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(kvp.Value, System.Text.Encoding.UTF8, "application/json")
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
