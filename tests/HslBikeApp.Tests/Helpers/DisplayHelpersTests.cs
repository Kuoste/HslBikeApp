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
    [InlineData(AvailabilityTrend.RapidDecrease, "\u21ca", "#d32f2f")]
    [InlineData(AvailabilityTrend.Decreasing, "\u2193", "#f57c00")]
    [InlineData(AvailabilityTrend.Increasing, "\u2191", "#388e3c")]
    [InlineData(AvailabilityTrend.RapidIncrease, "\u21c8", "#009688")]
    [InlineData(AvailabilityTrend.Stable, "", "")]
    public void GetBadge_ReturnsExpectedArrowAndColour(AvailabilityTrend trend, string expectedBadge, string expectedColour)
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
    [InlineData(AvailabilityTrend.RapidDecrease, "\u21ca")]
    [InlineData(AvailabilityTrend.Decreasing, "\u2193")]
    [InlineData(AvailabilityTrend.Increasing, "\u2191")]
    [InlineData(AvailabilityTrend.RapidIncrease, "\u21c8")]
    [InlineData(AvailabilityTrend.Stable, null)]
    public void GetTrendChevron_ReturnsExpectedSymbol(AvailabilityTrend trend, string? expectedChevron)
    {
        Assert.Equal(expectedChevron, DisplayHelpers.GetTrendChevron(trend));
    }

    [Fact]
    public void FormatTrendExplanation_WhenWindowIsLessThanOneMinute_ReturnsNull()
    {
        var summary = new TrendSummary(AvailabilityTrend.Increasing, 2, 0);

        Assert.Null(DisplayHelpers.FormatTrendExplanation(summary));
    }

    [Fact]
    public void FormatTrendExplanation_WhenStable_ReturnsNoChangeText()
    {
        var summary = new TrendSummary(AvailabilityTrend.Stable, 0, 15);

        Assert.Equal("No change in the last 15 min", DisplayHelpers.FormatTrendExplanation(summary));
    }

    [Fact]
    public void FormatTrendExplanation_WhenSingleBikeIncrease_UsesSingularWithPlusSign()
    {
        var summary = new TrendSummary(AvailabilityTrend.Increasing, 1, 12);

        Assert.Equal("+1 bike in the last 12 min", DisplayHelpers.FormatTrendExplanation(summary));
    }

    [Fact]
    public void FormatTrendExplanation_WhenBikesDecrease_UsesNegativeSign()
    {
        var summary = new TrendSummary(AvailabilityTrend.Decreasing, -2, 8);

        Assert.Equal("-2 bikes in the last 8 min", DisplayHelpers.FormatTrendExplanation(summary));
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
        Assert.StartsWith("\u2193", text);
    }

    [Fact]
    public void GetTrendText_Increasing_ContainsBikesArriving()
    {
        var text = DisplayHelpers.GetTrendText(AvailabilityTrend.Increasing);
        Assert.Contains("Bikes arriving", text);
        Assert.StartsWith("\u2191", text);
    }

    [Fact]
    public void GetTrendText_RapidDecrease_ContainsRapidly()
    {
        var text = DisplayHelpers.GetTrendText(AvailabilityTrend.RapidDecrease);
        Assert.Contains("rapidly", text);
        Assert.StartsWith("\u21ca", text);
    }

    [Fact]
    public void GetTrendText_RapidIncrease_ContainsRapidly()
    {
        var text = DisplayHelpers.GetTrendText(AvailabilityTrend.RapidIncrease);
        Assert.Contains("rapidly", text);
        Assert.StartsWith("\u21c8", text);
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
