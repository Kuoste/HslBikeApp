using Bunit;
using HslBikeApp.Components;
using HslBikeApp.Models;
using System.Globalization;

namespace HslBikeApp.Tests.Components;

public class StationDetailPanelTests : BunitContext
{
    [Fact]
    public void RendersTrendChipWithTooltip_WhenTrendIsIncreasing()
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

        var component = Render<StationDetailPanel>(parameters => parameters
            .Add(panel => panel.Station, station)
            .Add(panel => panel.Trend, AvailabilityTrend.Increasing)
            .Add(panel => panel.TrendSummary, new TrendSummary(AvailabilityTrend.Increasing, 2, 5))
            .Add(panel => panel.SnapshotCounts, new[] { 5, 7 })
            .Add(panel => panel.Timestamps, new[] { now.AddMinutes(-5), now }));

        var chip = component.Find(".trend-chip");
        Assert.Equal("\u2191", chip.TextContent.Trim());
        Assert.Contains("+2 bikes in the last 5 min", chip.GetAttribute("title") ?? "");
        // Explanation must not appear as visible text — only as tooltip.
        Assert.DoesNotContain("+2 bikes in the last 5 min", component.Find(".availability-card").TextContent);
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

        var component = Render<StationDetailPanel>(parameters => parameters
            .Add(panel => panel.Station, station)
            .Add(panel => panel.Trend, AvailabilityTrend.Stable)
            .Add(panel => panel.TrendSummary, new TrendSummary(AvailabilityTrend.Stable, 0, 5))
            .Add(panel => panel.SnapshotCounts, new[] { 7, 7 })
            .Add(panel => panel.Timestamps, new[] { now.AddMinutes(-5), now }));

        Assert.Empty(component.FindAll(".trend-chip"));
        Assert.DoesNotContain("No change in the last 5 min", component.Markup);
        Assert.DoesNotContain("View trip history", component.Markup);
    }

    [Fact]
    public void ChartUsesUnifiedTealColour()
    {
        var now = DateTime.UtcNow;
        var station = new BikeStation
        {
            Id = "001",
            Name = "Kamppi",
            Address = "Kamppi 1",
            Capacity = 20,
            BikesAvailable = 1,
            SpacesAvailable = 19,
            IsActive = true
        };

        var component = Render<StationDetailPanel>(parameters => parameters
            .Add(panel => panel.Station, station)
            .Add(panel => panel.Trend, AvailabilityTrend.Stable)
            .Add(panel => panel.TrendSummary, new TrendSummary(AvailabilityTrend.Stable, 0, 15))
            .Add(panel => panel.SnapshotCounts, new[] { 1, 1 })
            .Add(panel => panel.Timestamps, new[] { now.AddMinutes(-15), now }));

        Assert.NotEmpty(component.FindAll(".zone-teal"));
        Assert.Empty(component.FindAll(".zone-green"));
        Assert.Empty(component.FindAll(".zone-amber"));
        Assert.Empty(component.FindAll(".zone-red"));
    }

    [Fact]
    public void ChartLineExtendsToLiveDot_WhenLiveDataIsAvailable()
    {
        var snapshotStart = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var snapshotEnd = snapshotStart.AddMinutes(15);
        var liveTimestamp = snapshotStart.AddMinutes(42);
        var station = new BikeStation
        {
            Id = "001",
            Name = "Kamppi",
            Address = "Kamppi 1",
            Capacity = 20,
            BikesAvailable = 3,
            SpacesAvailable = 17,
            IsActive = true
        };

        var component = Render<StationDetailPanel>(parameters => parameters
            .Add(panel => panel.Station, station)
            .Add(panel => panel.Trend, AvailabilityTrend.Stable)
            .Add(panel => panel.TrendSummary, new TrendSummary(AvailabilityTrend.Stable, 0, 42))
            .Add(panel => panel.SnapshotCounts, new[] { 5, 5 })
            .Add(panel => panel.Timestamps, new[] { snapshotStart, snapshotEnd })
            .Add(panel => panel.LiveCount, 3)
            .Add(panel => panel.LiveTimestamp, liveTimestamp));

        // The polyline should have 3 points: snapshot[0], snapshot[1], and the live point.
        var polyline = component.Find("polyline.zone-teal");
        var pointsAttr = polyline.GetAttribute("points") ?? "";
        var pointCount = pointsAttr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(3, pointCount);
    }

    [Fact]
    public void RendersAvailabilityHistoryInsideAvailabilityCard()
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

        var component = Render<StationDetailPanel>(parameters => parameters
            .Add(panel => panel.Station, station)
            .Add(panel => panel.Trend, AvailabilityTrend.Stable)
            .Add(panel => panel.TrendSummary, new TrendSummary(AvailabilityTrend.Stable, 0, 5))
            .Add(panel => panel.SnapshotCounts, new[] { 5, 7 })
            .Add(panel => panel.Timestamps, new[] { now.AddMinutes(-15), now }));

        Assert.Contains("Availability", component.Find(".availability-card h4").TextContent);
        Assert.DoesNotContain(component.FindAll(".detail-card h4"), heading => heading.TextContent.Contains("Availability History", StringComparison.Ordinal));
        Assert.DoesNotContain("Last ", component.Markup);
    }
}
