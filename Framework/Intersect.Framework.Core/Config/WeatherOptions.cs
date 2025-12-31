using Intersect.Config;
using Newtonsoft.Json;

namespace Intersect.Config;

/// <summary>
/// Weather system configuration options
/// </summary>
public class WeatherOptions
{
    /// <summary>
    /// Enable automatic weather system
    /// </summary>
    [JsonProperty(Order = 1)]
    public bool EnableAutomaticWeather { get; set; } = true;

    /// <summary>
    /// Minimum time between weather changes (in minutes)
    /// </summary>
    [JsonProperty(Order = 2)]
    public int MinTimeBetweenChanges { get; set; } = 10;

    /// <summary>
    /// Maximum time between weather changes (in minutes)
    /// </summary>
    [JsonProperty(Order = 3)]
    public int MaxTimeBetweenChanges { get; set; } = 30;

    /// <summary>
    /// Chance of clear weather (no weather) when changing (0-100)
    /// </summary>
    [JsonProperty(Order = 4)]
    public int ClearWeatherChance { get; set; } = 40;

    /// <summary>
    /// Available weather types
    /// </summary>
    [JsonProperty(Order = 5)]
    public List<WeatherType> WeatherTypes { get; set; } = new()
    {
        new WeatherType
        {
            Id = "rain",
            Name = "Chuva",
            AnimationId = Guid.Parse("dcd0472c-264b-4e8f-9250-065fd54460c2"),
            XSpeed = 2,
            YSpeed = 3,
            Intensity = 50,
            MinDuration = 5,
            MaxDuration = 15,
            Chance = 30,
            CanOccurDay = true,
            CanOccurNight = true,
            Seasons = Array.Empty<string>()
        },
        new WeatherType
        {
            Id = "storm",
            Name = "Tempestade",
            AnimationId = Guid.Parse("dcd0472c-264b-4e8f-9250-065fd54460c2"),
            XSpeed = 5,
            YSpeed = 5,
            Intensity = 90,
            MinDuration = 3,
            MaxDuration = 10,
            Chance = 15,
            CanOccurDay = true,
            CanOccurNight = true,
            Seasons = Array.Empty<string>()
        },
        new WeatherType
        {
            Id = "snow",
            Name = "Neve",
            AnimationId = Guid.Parse("dcd0472c-264b-4e8f-9250-065fd54460c2"),
            XSpeed = 1,
            YSpeed = 2,
            Intensity = 60,
            MinDuration = 10,
            MaxDuration = 30,
            Chance = 20,
            CanOccurDay = true,
            CanOccurNight = true,
            Seasons = new[] { "winter" }
        }
    };
}
