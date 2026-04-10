using HslBikeApp.Helpers;
using HslBikeApp.Models;

namespace HslBikeApp.Tests.Helpers;

public class DisplayHelpersTests
{
    [Fact]
    public void GetMarkerColour_InactiveStation_ReturnsGrey()
    {
        var station = new BikeStation { Id = "1", Name = "Test", IsActive = false, BikesAvailable = 5 };
        Assert.Equal("#9e9e9e", DisplayHelpers.GetMarkerColour(station));
    }

    [Fact]
    public void GetMarkerColour_ZeroBikes_ReturnsRed()
    {
        var station = new BikeStation { Id = "1", Name = "Test", IsActive = true, BikesAvailable = 0 };
        Assert.Equal("#d32f2f", DisplayHelpers.GetMarkerColour(station));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetMarkerColour_LowBikes_ReturnsOrange(int bikes)
    {
        var station = new BikeStation { Id = "1", Name = "Test", IsActive = true, BikesAvailable = bikes };
        Assert.Equal("#f57c00", DisplayHelpers.GetMarkerColour(station));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(20)]
    public void GetMarkerColour_HighBikes_ReturnsGreen(int bikes)
    {
        var station = new BikeStation { Id = "1", Name = "Test", IsActive = true, BikesAvailable = bikes };
        Assert.Equal("#388e3c", DisplayHelpers.GetMarkerColour(station));
    }

    [Theory]
    [InlineData(AvailabilityTrend.RapidDecrease, "\u00bb\u00bb", "#d32f2f")]
    [InlineData(AvailabilityTrend.Decreasing, "\u00bb", "#f57c00")]
    [InlineData(AvailabilityTrend.Increasing, "\u00ab", "#388e3c")]
    [InlineData(AvailabilityTrend.RapidIncrease, "\u00ab\u00ab", "#009688")]
    [InlineData(AvailabilityTrend.Stable, "", "")]
    public void GetBadge_ReturnsExpectedChevronAndColour(AvailabilityTrend trend, string expectedBadge, string expectedColour)
    {
        var (badge, colour) = DisplayHelpers.GetBadge(trend);
        Assert.Equal(expectedBadge, badge);
        Assert.Equal(expectedColour, colour);
    }

    [Theory]
    [InlineData(AvailabilityTrend.RapidDecrease, "rapid-decrease")]
    [InlineData(AvailabilityTrend.Decreasing, "decreasing")]
    [InlineData(AvailabilityTrend.Increasing, "increasing")]
    [InlineData(AvailabilityTrend.RapidIncrease, "rapid-increase")]
    [InlineData(AvailabilityTrend.Stable, "stable")]
    public void GetTrendClass_ReturnsExpectedCssClass(AvailabilityTrend trend, string expectedClass)
    {
        Assert.Equal(expectedClass, DisplayHelpers.GetTrendClass(trend));
    }

    [Theory]
    [InlineData(AvailabilityTrend.RapidDecrease, "\u00bb\u00bb")]
    [InlineData(AvailabilityTrend.Decreasing, "\u00bb")]
    [InlineData(AvailabilityTrend.Increasing, "\u00ab")]
    [InlineData(AvailabilityTrend.RapidIncrease, "\u00ab\u00ab")]
    [InlineData(AvailabilityTrend.Stable, "=")]
    public void GetTrendChevron_ReturnsExpectedSymbol(AvailabilityTrend trend, string expectedChevron)
    {
        Assert.Equal(expectedChevron, DisplayHelpers.GetTrendChevron(trend));
    }

    [Fact]
    public void GetTrendText_Stable_ReturnsStable()
    {
        Assert.Equal("Stable", DisplayHelpers.GetTrendText(AvailabilityTrend.Stable));
    }

    [Fact]
    public void GetTrendText_Decreasing_ContainsBikesLeaving()
    {
        var text = DisplayHelpers.GetTrendText(AvailabilityTrend.Decreasing);
        Assert.Contains("Bikes leaving", text);
        Assert.StartsWith("\u00bb", text);
    }

    [Fact]
    public void GetTrendText_Increasing_ContainsBikesArriving()
    {
        var text = DisplayHelpers.GetTrendText(AvailabilityTrend.Increasing);
        Assert.Contains("Bikes arriving", text);
        Assert.StartsWith("\u00ab", text);
    }

    [Fact]
    public void GetTrendText_RapidDecrease_ContainsRapidly()
    {
        var text = DisplayHelpers.GetTrendText(AvailabilityTrend.RapidDecrease);
        Assert.Contains("rapidly", text);
    }

    [Fact]
    public void GetTrendText_RapidIncrease_ContainsRapidly()
    {
        var text = DisplayHelpers.GetTrendText(AvailabilityTrend.RapidIncrease);
        Assert.Contains("rapidly", text);
    }

    [Theory]
    [InlineData(0, "empty")]
    [InlineData(1, "bikes")]
    [InlineData(10, "bikes")]
    public void GetBikeChipClass_ReturnsExpectedClass(int bikesAvailable, string expectedClass)
    {
        Assert.Equal(expectedClass, DisplayHelpers.GetBikeChipClass(bikesAvailable));
    }
}
