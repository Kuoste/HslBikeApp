using HslBikeApp.Models;

namespace HslBikeApp.Tests.Models;

public class StationHistoryTests
{
    [Fact]
    public void AverageDurationFormatted_ReturnsMinutes()
    {
        var h = new StationHistory
        {
            DepartureStationId = "1",
            ArrivalStationId = "2",
            AverageDurationSeconds = 480
        };
        Assert.Equal("8 min", h.AverageDurationFormatted);
    }

    [Fact]
    public void AverageDurationFormatted_LessThanOneMinute()
    {
        var h = new StationHistory
        {
            DepartureStationId = "1",
            ArrivalStationId = "2",
            AverageDurationSeconds = 30
        };
        Assert.Equal("<1 min", h.AverageDurationFormatted);
    }

    [Fact]
    public void AverageDurationFormatted_ExactlyOneMinute()
    {
        var h = new StationHistory
        {
            DepartureStationId = "1",
            ArrivalStationId = "2",
            AverageDurationSeconds = 60
        };
        Assert.Equal("1 min", h.AverageDurationFormatted);
    }

    [Fact]
    public void AverageDurationFormatted_WhenDurationHasFraction_TruncatesToWholeMinutes()
    {
        var h = new StationHistory
        {
            DepartureStationId = "1",
            ArrivalStationId = "2",
            AverageDurationSeconds = 89.9
        };

        Assert.Equal("1 min", h.AverageDurationFormatted);
    }

    [Fact]
    public void DefaultValues()
    {
        var h = new StationHistory
        {
            DepartureStationId = "1",
            ArrivalStationId = "2"
        };
        Assert.Equal("", h.ArrivalStationName);
        Assert.Equal(0, h.TripCount);
        Assert.Equal(0d, h.AverageDistanceMetres);
    }
}
