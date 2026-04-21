namespace HslBikeApp.Models;

public record TrendSummary(
    AvailabilityTrend Trend,
    int DeltaBikes,
    int WindowMinutes);
