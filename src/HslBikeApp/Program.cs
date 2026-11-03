using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HslBikeApp;
using HslBikeApp.Http;
using HslBikeApp.Services;
using HslBikeApp.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var config = builder.Configuration;
// An empty AggregatorBaseUrl means no backend is running (e.g. off-season); AppState then shows a notice.
var aggregatorBaseUrl = config["AggregatorBaseUrl"] ?? "";

var httpClientWithHeaders = AggregatorHttpClient.Create();
var plainHttpClient = new HttpClient();

builder.Services.AddSingleton(new StationService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton(new StatisticsService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton(new CycleLaneService(plainHttpClient));
builder.Services.AddSingleton(new SnapshotService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton(new LiveStationService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton(new OpenDataService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton<AppState>();

await builder.Build().RunAsync();
