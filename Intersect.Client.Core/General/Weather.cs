using Intersect.Client.Core;
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

    public static void LoadWeather(Guid animationId, int xSpeed, int ySpeed, int intensity, string sound, float soundVolume)
    {
        ApplicationContext.CurrentContext.Logger.LogDebug(
            $"[Weather] Loading weather: AnimationId={animationId}, Intensity={intensity}"
        );

        _animationId = animationId;
        _xSpeed = xSpeed;
        _ySpeed = ySpeed;
        _intensity = intensity;
    }

    /// <summary>
    /// This method is kept for compatibility but does nothing since sound is removed
    /// </summary>
    public static void SetIndoorStatus(bool isIndoors)
    {
        // Sound system removed - this method is kept for compatibility
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
