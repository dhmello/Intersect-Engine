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
    public List<WeatherType> WeatherTypes { get; set; } = new();
}
