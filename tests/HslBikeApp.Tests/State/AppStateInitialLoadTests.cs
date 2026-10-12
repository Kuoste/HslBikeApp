using System.Net;
using System.Text;
using HslBikeApp.Services;
using HslBikeApp.State;
using Microsoft.JSInterop;

namespace HslBikeApp.Tests.State;

/// <summary>
/// Covers how <see cref="AppState"/> loads stations when the page opens, including a cold backend.
/// </summary>
public class AppStateInitialLoadTests
{
    private const string StationsJson =
        "[{\"id\":\"001\",\"name\":\"Kaivopuisto\",\"lat\":60.15,\"lon\":24.95,\"capacity\":20," +
        "\"bikesAvailable\":7,\"spacesAvailable\":13,\"isActive\":true}]";

    private const string SnapshotsJson =
        """{"intervalMinutes":15,"timestamps":["2026-06-01T10:00:00Z"],"rows":[["001",5]]}""";

    [Fact]
    public async Task InitAsync_DoesNotWaitForSnapshotsBeforeLoadingStations()
    {
        var releaseSnapshots = new TaskCompletionSource();
        await using var state = CreateAppState(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/snapshots"))
            {
                await releaseSnapshots.Task;
                return Json(SnapshotsJson);
            }

            return Route(request);
        });

        var initTask = state.InitAsync();
        await WaitUntilAsync(() => state.Stations.Count > 0);

        Assert.False(initTask.IsCompleted);
        Assert.Equal("Kaivopuisto", Assert.Single(state.Stations).Name);

        releaseSnapshots.SetResult();
        await initTask;

        Assert.Single(state.SnapshotTimestamps);
    }

    [Fact]
    public async Task InitAsync_WhenBackendIsWakingUp_RetriesAndShowsStatusInsteadOfError()
    {
        var stationCalls = 0;
        await using var state = CreateAppState(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/stations") && Interlocked.Increment(ref stationCalls) <= 2)
                throw new TaskCanceledException("The request was cancelled due to the configured HttpClient.Timeout.");

            return Task.FromResult(Route(request));
        });
        var observedErrors = new List<string>();
        var observedStatuses = new List<string>();
        state.OnStateChanged += () =>
        {
            if (state.StationError is { } error) observedErrors.Add(error);
            if (state.StationStatusMessage is { } status) observedStatuses.Add(status);
        };

        await state.InitAsync();

        Assert.Equal(3, stationCalls);
        Assert.Empty(observedErrors);
        Assert.Contains(AppState.LoadingStationsMessage, observedStatuses);
        Assert.Contains(AppState.WakingUpMessage, observedStatuses);
        Assert.Single(state.Stations);
        Assert.Null(state.StationError);
        Assert.Null(state.StationStatusMessage);
    }

    [Fact]
    public async Task InitAsync_WhenAllAttemptsFail_ReportsErrorAfterLastRetry()
    {
        var stationCalls = 0;
        await using var state = CreateAppState(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/stations"))
            {
                Interlocked.Increment(ref stationCalls);
                throw new HttpRequestException("network error");
            }

            return Task.FromResult(Route(request));
        });
        var errorsBeforeLastAttempt = 0;
        state.OnStateChanged += () =>
        {
            if (state.StationError is not null && stationCalls <= state.InitialLiveRetryDelays.Count)
                errorsBeforeLastAttempt++;
        };

        await state.InitAsync();

        Assert.Equal(state.InitialLiveRetryDelays.Count + 1, stationCalls);
        Assert.Equal(0, errorsBeforeLastAttempt);
        Assert.NotNull(state.StationError);
        Assert.Null(state.StationStatusMessage);
    }

    [Fact]
    public async Task InitAsync_WhenSnapshotsTimeOut_StillLoadsStations()
    {
        await using var state = CreateAppState(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/snapshots"))
                throw new TaskCanceledException("The request was cancelled due to the configured HttpClient.Timeout.");

            return Task.FromResult(Route(request));
        });

        await state.InitAsync();

        Assert.Single(state.Stations);
        Assert.Empty(state.SnapshotTimestamps);
        Assert.Null(state.StationError);
    }

    [Fact]
    public async Task RefreshLiveAsync_WhenPollFailsAfterSuccessfulLoad_KeepsStationsAndReportsError()
    {
        var stationCalls = 0;
        await using var state = CreateAppState(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/stations") && Interlocked.Increment(ref stationCalls) > 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            return Task.FromResult(Route(request));
        });

        await state.RefreshLiveAsync();
        await state.RefreshLiveAsync();

        Assert.Equal("Kaivopuisto", Assert.Single(state.Stations).Name);
        Assert.NotNull(state.StationError);
        Assert.Null(state.StationStatusMessage);
    }

    private static HttpResponseMessage Route(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/api/stations")) return Json(StationsJson);
        if (path.EndsWith("/api/snapshots")) return Json(SnapshotsJson);
        if (path.EndsWith("/api/open-data")) return Json("[]");
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static AppState CreateAppState(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        const string baseUrl = "https://test.local";
        var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) => handler(request)));

        return new AppState(
            new StationService(httpClient, baseUrl),
            new StatisticsService(httpClient, baseUrl),
            new CycleLaneService(httpClient),
            new SnapshotService(httpClient, baseUrl),
            new LiveStationService(httpClient, baseUrl),
            new OpenDataService(httpClient, baseUrl),
            new StubJsRuntime())
        {
            InitialLiveRetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero]
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met within 5 seconds.");
            await Task.Delay(10);
        }
    }

    private sealed class StubJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }
}
