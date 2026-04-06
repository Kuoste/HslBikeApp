using System.Net;
using System.Text;
using HslBikeApp.Services;

namespace HslBikeApp.Tests.Services;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task FetchAvailabilityAsync_WhenResponseIsSuccessful_ReturnsTwentyFourHourlyBuckets()
    {
        HttpRequestMessage? capturedRequest = null;
        var responseJson =
            """
            [
            { "hour": 0, "averageBikesAvailable": 0.5 },
            { "hour": 1, "averageBikesAvailable": 1.5 },
            { "hour": 2, "averageBikesAvailable": 2.5 },
            { "hour": 3, "averageBikesAvailable": 3.5 },
            { "hour": 4, "averageBikesAvailable": 4.5 },
            { "hour": 5, "averageBikesAvailable": 5.5 },
            { "hour": 6, "averageBikesAvailable": 6.5 },
            { "hour": 7, "averageBikesAvailable": 7.5 },
            { "hour": 8, "averageBikesAvailable": 8.5 },
            { "hour": 9, "averageBikesAvailable": 9.5 },
            { "hour": 10, "averageBikesAvailable": 10.5 },
            { "hour": 11, "averageBikesAvailable": 11.5 },
            { "hour": 12, "averageBikesAvailable": 12.5 },
            { "hour": 13, "averageBikesAvailable": 13.5 },
            { "hour": 14, "averageBikesAvailable": 14.5 },
            { "hour": 15, "averageBikesAvailable": 15.5 },
            { "hour": 16, "averageBikesAvailable": 16.5 },
            { "hour": 17, "averageBikesAvailable": 17.5 },
            { "hour": 18, "averageBikesAvailable": 18.5 },
            { "hour": 19, "averageBikesAvailable": 19.5 },
            { "hour": 20, "averageBikesAvailable": 20.5 },
            { "hour": 21, "averageBikesAvailable": 21.5 },
            { "hour": 22, "averageBikesAvailable": 22.5 },
            { "hour": 23, "averageBikesAvailable": 23.5 }
            ]
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }));
        var service = new AvailabilityService(httpClient, "https://aggregator.example/");

        var availability = await service.FetchAvailabilityAsync("001");

        Assert.NotNull(capturedRequest);
        Assert.Equal("https://aggregator.example/api/stations/001/availability", capturedRequest.RequestUri?.ToString());
        Assert.Equal(24, availability.Count);
        Assert.Equal(0, availability[0].Hour);
        Assert.Equal(23.5, availability[23].AverageBikesAvailable);
    }

    [Fact]
    public async Task FetchAvailabilityAsync_WhenResponseIsEmpty_ReturnsEmptyList()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        })));
        var service = new AvailabilityService(httpClient, "https://aggregator.example");

        var availability = await service.FetchAvailabilityAsync("001");

        Assert.Empty(availability);
    }

    [Fact]
    public async Task FetchAvailabilityAsync_WhenResponseIsNotFound_ReturnsEmptyList()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var service = new AvailabilityService(httpClient, "https://aggregator.example");

        var availability = await service.FetchAvailabilityAsync("001");

        Assert.Empty(availability);
    }
}
