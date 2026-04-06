namespace HslBikeApp.Models;

public record StationHistory
{
    public required string DepartureStationId { get; init; }
    public required string ArrivalStationId { get; init; }
    public string ArrivalStationName { get; init; } = "";
    public int TripCount { get; init; }
    public double AverageDurationSeconds { get; init; }
    public double AverageDistanceMetres { get; init; }

    public string AverageDurationFormatted
    {
        get
        {
            var minutes = (int)(AverageDurationSeconds / 60);
            return minutes < 1 ? "<1 min" : $"{minutes} min";
        }
    }
}
