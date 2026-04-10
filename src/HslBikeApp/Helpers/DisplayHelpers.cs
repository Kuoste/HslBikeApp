using HslBikeApp.Models;

namespace HslBikeApp.Helpers;

/// <summary>
/// Pure helper methods for station marker colours, trend chevrons and CSS classes.
/// Shared across MapView, StationInfoPanel and StationDetailPanel.
/// </summary>
public static class DisplayHelpers
{
    /// <summary>Returns the marker fill colour for the given station.</summary>
    public static string GetMarkerColour(BikeStation station)
    {
        if (!station.IsActive) return "#9e9e9e";        // grey
        if (station.BikesAvailable == 0) return "#d32f2f"; // red
        if (station.BikesAvailable <= 3) return "#f57c00";  // orange
        return "#388e3c";                                   // green
    }

    /// <summary>Returns a chevron badge string and its colour for map marker badges.</summary>
    public static (string Badge, string Colour) GetBadge(AvailabilityTrend trend) => trend switch
    {
        AvailabilityTrend.RapidDecrease => ("\u00bb\u00bb", "#d32f2f"),
        AvailabilityTrend.Decreasing => ("\u00bb", "#f57c00"),
        AvailabilityTrend.Increasing => ("\u00ab", "#388e3c"),
        AvailabilityTrend.RapidIncrease => ("\u00ab\u00ab", "#009688"),
        _ => ("", "")
    };

    /// <summary>Returns a CSS modifier class name for the given trend.</summary>
    public static string GetTrendClass(AvailabilityTrend trend) => trend switch
    {
        AvailabilityTrend.RapidDecrease => "rapid-decrease",
        AvailabilityTrend.Decreasing => "decreasing",
        AvailabilityTrend.Increasing => "increasing",
        AvailabilityTrend.RapidIncrease => "rapid-increase",
        _ => "stable"
    };

    /// <summary>Returns a short chevron symbol for the trend (info panel chip).</summary>
    public static string GetTrendChevron(AvailabilityTrend trend) => trend switch
    {
        AvailabilityTrend.RapidDecrease => "\u00bb\u00bb",
        AvailabilityTrend.Decreasing => "\u00bb",
        AvailabilityTrend.Increasing => "\u00ab",
        AvailabilityTrend.RapidIncrease => "\u00ab\u00ab",
        _ => "="
    };

    /// <summary>Returns descriptive trend text with chevron prefix (detail panel).</summary>
    public static string GetTrendText(AvailabilityTrend trend) => trend switch
    {
        AvailabilityTrend.RapidDecrease => "\u00bb\u00bb Bikes leaving rapidly",
        AvailabilityTrend.Decreasing => "\u00bb Bikes leaving",
        AvailabilityTrend.Increasing => "\u00ab Bikes arriving",
        AvailabilityTrend.RapidIncrease => "\u00ab\u00ab Bikes arriving rapidly",
        _ => "Stable"
    };

    /// <summary>Returns a CSS class for the bike count chip.</summary>
    public static string GetBikeChipClass(int bikesAvailable) =>
        bikesAvailable == 0 ? "empty" : "bikes";
}
