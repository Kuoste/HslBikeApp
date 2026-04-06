using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HslBikeApp;
using HslBikeApp.Services;
using HslBikeApp.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var config = builder.Configuration;
var aggregatorBaseUrl = config["AggregatorBaseUrl"];
if (string.IsNullOrWhiteSpace(aggregatorBaseUrl))
    aggregatorBaseUrl = "https://kuoste.github.io/hsl-bike-data-aggregator";

var plainHttp = new HttpClient();

builder.Services.AddSingleton(new StationService(plainHttp, aggregatorBaseUrl));
builder.Services.AddSingleton(new AvailabilityService(plainHttp, aggregatorBaseUrl));
builder.Services.AddSingleton(new HistoryService(plainHttp, aggregatorBaseUrl));
builder.Services.AddSingleton(new CycleLaneService(plainHttp));
builder.Services.AddSingleton(new SnapshotService(plainHttp, aggregatorBaseUrl));
builder.Services.AddSingleton<AppState>();

await builder.Build().RunAsync();
