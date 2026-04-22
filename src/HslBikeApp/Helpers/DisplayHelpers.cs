using HslBikeApp.Models;

namespace HslBikeApp.Helpers;

/// <summary>
/// Pure helper methods for station marker colours, trend arrows and CSS classes.
/// Shared across MapView and station panels.
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

    /// <summary>Returns an arrow badge string and its colour for map marker badges.</summary>
    public static (string Badge, string Colour) GetBadge(AvailabilityTrend trend) => trend switch
    {
        AvailabilityTrend.RapidDecrease => ("\u21ca", "#d32f2f"),
        AvailabilityTrend.Decreasing => ("\u2193", "#f57c00"),
        AvailabilityTrend.Increasing => ("\u2191", "#388e3c"),
        AvailabilityTrend.RapidIncrease => ("\u21c8", "#009688"),
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

    /// <summary>Returns a short arrow symbol for the trend chip, or <c>null</c> for stable.</summary>
    public static string? GetTrendChevron(AvailabilityTrend trend) => trend switch
    {
        AvailabilityTrend.RapidDecrease => "\u21ca",
        AvailabilityTrend.Decreasing => "\u2193",
        AvailabilityTrend.Increasing => "\u2191",
        AvailabilityTrend.RapidIncrease => "\u21c8",
        _ => null
    };

    /// <summary>Returns a human-readable explanation for the recent trend window.</summary>
    public static string? FormatTrendExplanation(TrendSummary summary)
    {
        if (summary.WindowMinutes < 1)
            return null;

        if (summary.Trend == AvailabilityTrend.Stable || summary.DeltaBikes == 0)
            return $"No change in the last {summary.WindowMinutes} min";

        var magnitude = Math.Abs(summary.DeltaBikes);
        var noun = magnitude == 1 ? "bike" : "bikes";
        var sign = summary.DeltaBikes > 0 ? "+" : string.Empty;
        return $"{sign}{summary.DeltaBikes} {noun} in the last {summary.WindowMinutes} min";
    }

    /// <summary>Returns descriptive trend text with arrow prefix.</summary>
    public static string GetTrendText(AvailabilityTrend trend) => trend switch
    {
        AvailabilityTrend.RapidDecrease => "\u21ca Bikes leaving rapidly",
        AvailabilityTrend.Decreasing => "\u2193 Bikes leaving",
        AvailabilityTrend.Increasing => "\u2191 Bikes arriving",
        AvailabilityTrend.RapidIncrease => "\u21c8 Bikes arriving rapidly",
        _ => "Stable"
    };

    /// <summary>Returns a CSS class for the bike count chip.</summary>
    public static string GetBikeChipClass(int bikesAvailable) =>
        bikesAvailable == 0 ? "empty" : "bikes";
}
