using System.Net;
using HslBikeApp.Http;
using HslBikeApp.Tests;

namespace HslBikeApp.Tests.Http;

public class AggregatorHttpClientTests
{
    [Fact]
    public void Create_SetsRequestTimeoutBelowHttpClientDefault()
    {
        using var client = AggregatorHttpClient.Create();

        Assert.Equal(AggregatorHttpClient.RequestTimeout, client.Timeout);
        Assert.True(client.Timeout < TimeSpan.FromSeconds(100));
    }

    [Fact]
    public async Task Create_AddsXAppSourceHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = AggregatorHttpClient.Create(new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));

        await client.GetAsync("https://example.com/api/stations");

        Assert.NotNull(capturedRequest);
        Assert.Equal("HslBikeApp", Assert.Single(capturedRequest.Headers.GetValues("X-App-Source")));
    }
}
