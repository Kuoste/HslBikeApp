using System.Net;
using HslBikeApp.Http;
using HslBikeApp.Tests;

namespace HslBikeApp.Tests.Http;

public class AppSourceHeaderHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsXAppSourceHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new StubHttpMessageHandler((req, ct) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new AppSourceHeaderHandler(innerHandler);
        var client = new HttpClient(handler);

        await client.GetAsync("https://example.com/api/test");

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("X-App-Source"));
        var headerValues = capturedRequest.Headers.GetValues("X-App-Source");
        Assert.Equal("HslBikeApp", Assert.Single(headerValues));
    }

    [Fact]
    public async Task SendAsync_AddsHeaderToMultipleRequests()
    {
        var requestCount = 0;
        HttpRequestMessage? lastRequest = null;

        var innerHandler = new StubHttpMessageHandler((req, ct) =>
        {
            requestCount++;
            lastRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new AppSourceHeaderHandler(innerHandler);
        var client = new HttpClient(handler);

        await client.GetAsync("https://example.com/api/first");
        await client.GetAsync("https://example.com/api/second");
        await client.GetAsync("https://example.com/api/third");

        Assert.Equal(3, requestCount);
        Assert.NotNull(lastRequest);
        Assert.True(lastRequest.Headers.Contains("X-App-Source"));
        var headerValues = lastRequest.Headers.GetValues("X-App-Source");
        Assert.Equal("HslBikeApp", Assert.Single(headerValues));
    }

    [Fact]
    public async Task SendAsync_PreservesOtherHeaders()
    {
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new StubHttpMessageHandler((req, ct) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new AppSourceHeaderHandler(innerHandler);
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("Custom-Header", "CustomValue");

        await client.GetAsync("https://example.com/api/test");

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("X-App-Source"));
        Assert.True(capturedRequest.Headers.Contains("Custom-Header"));
        Assert.Equal("CustomValue", Assert.Single(capturedRequest.Headers.GetValues("Custom-Header")));
    }
}
