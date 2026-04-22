using Bunit;
using HslBikeApp.Components;
using HslBikeApp.Models;

namespace HslBikeApp.Tests.Components;

public class StationDetailPanelTests : TestContext
{
    [Fact]
    public void RendersTrendChipAndExplanation_WhenTrendIsIncreasing()
    {
        var now = DateTime.UtcNow;
        var station = new BikeStation
        {
            Id = "001",
            Name = "Kamppi",
            Address = "Kamppi 1",
            Capacity = 20,
            BikesAvailable = 7,
            SpacesAvailable = 13,
            IsActive = true
        };

        var component = RenderComponent<StationDetailPanel>(parameters => parameters
            .Add(panel => panel.Station, station)
            .Add(panel => panel.Trend, AvailabilityTrend.Increasing)
            .Add(panel => panel.TrendSummary, new TrendSummary(AvailabilityTrend.Increasing, 2, 5))
            .Add(panel => panel.SnapshotCounts, new[] { 5, 7 })
            .Add(panel => panel.Timestamps, new[] { now.AddMinutes(-5), now }));

        Assert.Equal("↑", component.Find(".trend-chip").TextContent.Trim());
        Assert.Contains("+2 bikes in the last 5 min", component.Markup);
        Assert.Empty(component.FindAll(".sparkline"));
    }

    [Fact]
    public void DoesNotRenderTrendChip_WhenTrendIsStable()
    {
        var now = DateTime.UtcNow;
        var station = new BikeStation
        {
            Id = "001",
            Name = "Kamppi",
            Address = "Kamppi 1",
            Capacity = 20,
            BikesAvailable = 7,
            SpacesAvailable = 13,
            IsActive = true
        };

        var component = RenderComponent<StationDetailPanel>(parameters => parameters
            .Add(panel => panel.Station, station)
            .Add(panel => panel.Trend, AvailabilityTrend.Stable)
            .Add(panel => panel.TrendSummary, new TrendSummary(AvailabilityTrend.Stable, 0, 5))
            .Add(panel => panel.SnapshotCounts, new[] { 7, 7 })
            .Add(panel => panel.Timestamps, new[] { now.AddMinutes(-5), now }));

        Assert.Empty(component.FindAll(".trend-chip"));
        Assert.Contains("No change in the last 5 min", component.Markup);
        Assert.DoesNotContain("View trip history", component.Markup);
    }
}
