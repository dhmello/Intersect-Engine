using Intersect.Client.Core;
using Intersect.Client.Core.Sounds;
using Intersect.Client.Framework.Core.Sounds;
using Intersect.Core;
using Intersect.Framework.Core.GameObjects.Animations;
using Microsoft.Extensions.Logging;

namespace Intersect.Client.General;

public static class Weather
{
    private static Guid _animationId = Guid.Empty;
    private static int _xSpeed = 0;
    private static int _ySpeed = 0;
    private static int _intensity = 0;
    private static string _sound = string.Empty;
    private static float _soundVolume = 0.5f;
    private static ISound? _currentWeatherSound = null;

    public static void LoadWeather(Guid animationId, int xSpeed, int ySpeed, int intensity, string sound, float soundVolume)
    {
        ApplicationContext.CurrentContext.Logger.LogDebug(
            $"[Weather] Loading weather: AnimationId={animationId}, Sound='{sound}', Volume={soundVolume}, Intensity={intensity}"
        );

        // Check if weather is being cleared (empty animation)
        var isClearing = animationId == Guid.Empty || intensity <= 0;

        // Stop previous weather sound if it changed OR if clearing weather
        if ((_sound != sound || isClearing) && _currentWeatherSound != null)
        {
            ApplicationContext.CurrentContext.Logger.LogDebug($"[Weather] Stopping previous sound: '{_sound}'");
            Audio.StopSound(_currentWeatherSound as IMapSound);
            _currentWeatherSound = null;
        }

        _animationId = animationId;
        _xSpeed = xSpeed;
        _ySpeed = ySpeed;
        _intensity = intensity;
        _sound = sound;
        _soundVolume = soundVolume;

        // Only start new weather sound if NOT clearing
        if (!isClearing && !string.IsNullOrEmpty(sound))
        {
            ApplicationContext.CurrentContext.Logger.LogDebug($"[Weather] Attempting to play sound: '{sound}'");
            _currentWeatherSound = Audio.AddGameSound(sound, true);

            if (_currentWeatherSound != null)
            {
                ApplicationContext.CurrentContext.Logger.LogDebug("[Weather] Sound started successfully!");
            }
            else
            {
                ApplicationContext.CurrentContext.Logger.LogWarning($"[Weather] Failed to start sound: '{sound}'");
            }
        }
        else if (isClearing)
        {
            ApplicationContext.CurrentContext.Logger.LogDebug("[Weather] Weather cleared, no sound to play");
        }
        else if (string.IsNullOrEmpty(sound))
        {
            ApplicationContext.CurrentContext.Logger.LogDebug("[Weather] No sound specified for this weather");
        }
    }

    public static AnimationDescriptor? GetWeatherAnimation()
    {
        if (_animationId == Guid.Empty)
        {
            return null;
        }

        return AnimationDescriptor.Get(_animationId);
    }

    public static int GetWeatherXSpeed() => _xSpeed;
    public static int GetWeatherYSpeed() => _ySpeed;
    public static int GetWeatherIntensity() => _intensity;
}
