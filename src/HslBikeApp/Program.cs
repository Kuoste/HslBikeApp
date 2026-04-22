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
var aggregatorBaseUrl = config["AggregatorBaseUrl"];
if (string.IsNullOrWhiteSpace(aggregatorBaseUrl))
    aggregatorBaseUrl = "https://kuoste.github.io/hsl-bike-data-aggregator";

var httpClientWithHeaders = new HttpClient(new AppSourceHeaderHandler());
var plainHttpClient = new HttpClient();

builder.Services.AddSingleton(new StationService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton(new StatisticsService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton(new CycleLaneService(plainHttpClient));
builder.Services.AddSingleton(new SnapshotService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton(new LiveStationService(httpClientWithHeaders, aggregatorBaseUrl));
builder.Services.AddSingleton<AppState>();

await builder.Build().RunAsync();
