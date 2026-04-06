namespace HslBikeApp.Models;

public record HourlyAvailability
{
    public int Hour { get; init; }
    public double AverageBikesAvailable { get; init; }
}
