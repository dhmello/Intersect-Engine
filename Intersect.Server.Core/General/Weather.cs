using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Utilities;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.General;

public static class Weather
{
    private static Guid _currentAnimationId = Guid.Empty;
    private static int _currentXSpeed = 0;
    private static int _currentYSpeed = 0;
    private static int _currentIntensity = 0;

    // Automatic weather system
    private static long _nextWeatherChangeTime = 0;
    private static long _currentWeatherEndTime = 0;
    private static Random _random = new Random();

    public static void Init()
    {
        // Initialize with clear weather
        SetWeather(Guid.Empty, 0, 0, 0);
        
        // Schedule first weather check
        if (Options.Instance.Weather.EnableAutomaticWeather)
        {
            ScheduleNextWeatherChange();
        }
    }

    public static void Update()
    {
        if (!Options.Instance.Weather.EnableAutomaticWeather)
        {
            return;
        }

        var currentTime = Timing.Global.MillisecondsUtc;

        // Check if it's time to change weather
        if (currentTime >= _nextWeatherChangeTime)
        {
            ChangeWeatherAutomatically();
        }
    }

    private static void ChangeWeatherAutomatically()
    {
        var weatherOptions = Options.Instance.Weather;
        
        // Decide if we should have clear weather or actual weather
        var clearChance = _random.Next(0, 100);
        if (clearChance < weatherOptions.ClearWeatherChance)
        {
            // Clear weather
            SetWeather(Guid.Empty, 0, 0, 0);
            ScheduleNextWeatherChange();
            return;
        }

        // Get current time of day
        var currentTime = Time.GetTime();
        var isDay = currentTime.Hour >= 6 && currentTime.Hour < 18;

        // Filter available weather types
        var availableWeathers = weatherOptions.WeatherTypes
            .Where(w => isDay ? w.CanOccurDay : w.CanOccurNight)
            .ToList();

        if (availableWeathers.Count == 0)
        {
            SetWeather(Guid.Empty, 0, 0, 0);
            ScheduleNextWeatherChange();
            return;
        }

        // Calculate total chance
        var totalChance = availableWeathers.Sum(w => w.Chance);
        var randomValue = _random.Next(0, totalChance);

        // Select weather based on chance
        Config.WeatherType? selectedWeather = null;
        var currentChance = 0;
        foreach (var weather in availableWeathers)
        {
            currentChance += weather.Chance;
            if (randomValue < currentChance)
            {
                selectedWeather = weather;
                break;
            }
        }

        if (selectedWeather != null)
        {
            // Set the weather (sound parameters removed)
            SetWeather(
                selectedWeather.AnimationId,
                selectedWeather.XSpeed,
                selectedWeather.YSpeed,
                selectedWeather.Intensity
            );

            // Schedule weather end
            var duration = _random.Next(selectedWeather.MinDuration, selectedWeather.MaxDuration + 1);
            _currentWeatherEndTime = Timing.Global.MillisecondsUtc + (duration * 60 * 1000);
            _nextWeatherChangeTime = _currentWeatherEndTime;

            ApplicationContext.Context.Value?.Logger.LogDebug(
                $"Weather changed to {selectedWeather.Name} for {duration} minutes"
            );
        }
        else
        {
            SetWeather(Guid.Empty, 0, 0, 0);
            ScheduleNextWeatherChange();
        }
    }

    private static void ScheduleNextWeatherChange()
    {
        var weatherOptions = Options.Instance.Weather;
        var delayMinutes = _random.Next(
            weatherOptions.MinTimeBetweenChanges,
            weatherOptions.MaxTimeBetweenChanges + 1
        );
        
        _nextWeatherChangeTime = Timing.Global.MillisecondsUtc + (delayMinutes * 60 * 1000);
    }

    public static void SetWeather(Guid animationId, int xSpeed, int ySpeed, int intensity)
    {
        _currentAnimationId = animationId;
        _currentXSpeed = xSpeed;
        _currentYSpeed = ySpeed;
        _currentIntensity = intensity;
    }

    public static Guid GetWeatherAnimationId() => _currentAnimationId;
    public static int GetWeatherXSpeed() => _currentXSpeed;
    public static int GetWeatherYSpeed() => _currentYSpeed;
    public static int GetWeatherIntensity() => _currentIntensity;
}
