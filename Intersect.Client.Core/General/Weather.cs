using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.GameObjects;

namespace Intersect.Client.General;

public static partial class Weather
{
    private static Guid sWeatherAnimationId = Guid.Empty;
    private static int sWeatherXSpeed = 0;
    private static int sWeatherYSpeed = 0;
    private static int sWeatherIntensity = 0;

    public static void LoadWeather(Guid animationId, int xSpeed, int ySpeed, int intensity)
    {
        sWeatherAnimationId = animationId;
        sWeatherXSpeed = xSpeed;
        sWeatherYSpeed = ySpeed;
        sWeatherIntensity = intensity;
    }

    public static Guid GetWeatherAnimationId()
    {
        return sWeatherAnimationId;
    }

    public static int GetWeatherXSpeed()
    {
        return sWeatherXSpeed;
    }

    public static int GetWeatherYSpeed()
    {
        return sWeatherYSpeed;
    }

    public static int GetWeatherIntensity()
    {
        return sWeatherIntensity;
    }

    public static AnimationDescriptor? GetWeatherAnimation()
    {
        if (sWeatherAnimationId == Guid.Empty)
        {
            return null;
        }

        return AnimationDescriptor.Get(sWeatherAnimationId);
    }
}
