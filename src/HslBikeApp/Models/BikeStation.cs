using System.Text.Json.Serialization;

namespace HslBikeApp.Models;

public record BikeStation
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Address { get; init; } = "";

    [JsonPropertyName("lat")]
    public double Latitude { get; init; }

    [JsonPropertyName("lon")]
    public double Longitude { get; init; }
    public int Capacity { get; init; }
    public int BikesAvailable { get; init; }
    public int SpacesAvailable { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime? LastUpdated { get; init; }

    [JsonIgnore]
    public double Occupancy => Capacity > 0 ? (double)BikesAvailable / Capacity : 0;

    [JsonIgnore]
    public bool IsEmpty => BikesAvailable == 0;

    [JsonIgnore]
    public bool IsFull => SpacesAvailable == 0;
}
