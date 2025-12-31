namespace Intersect.Config;

/// <summary>
/// Represents a weather type configuration
/// </summary>
public class WeatherType
{
    /// <summary>
    /// Unique identifier for this weather type
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the weather
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Animation ID (GUID) to display
    /// </summary>
    public Guid AnimationId { get; set; } = Guid.Empty;

    /// <summary>
    /// Horizontal particle speed
    /// </summary>
    public int XSpeed { get; set; } = 2;

    /// <summary>
    /// Vertical particle speed
    /// </summary>
    public int YSpeed { get; set; } = 3;

    /// <summary>
    /// Weather intensity (0-100)
    /// </summary>
    public int Intensity { get; set; } = 50;

    /// <summary>
    /// Minimum duration in minutes
    /// </summary>
    public int MinDuration { get; set; } = 5;

    /// <summary>
    /// Maximum duration in minutes
    /// </summary>
    public int MaxDuration { get; set; } = 15;

    /// <summary>
    /// Chance of occurring (0-100)
    /// </summary>
    public int Chance { get; set; } = 20;

    /// <summary>
    /// Can this weather occur during day?
    /// </summary>
    public bool CanOccurDay { get; set; } = true;

    /// <summary>
    /// Can this weather occur during night?
    /// </summary>
    public bool CanOccurNight { get; set; } = true;

    /// <summary>
    /// Seasons when this weather can occur (empty = all seasons)
    /// </summary>
    public string[] Seasons { get; set; } = Array.Empty<string>();
}
