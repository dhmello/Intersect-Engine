using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.GameObjects;
using Intersect.Server.Networking;
using Intersect.Utilities;

namespace Intersect.Server.General;

public static partial class Weather
{
    private static Guid sWeatherAnimationId = Guid.Empty;
    private static int sWeatherXSpeed = 0;
    private static int sWeatherYSpeed = 0;
    private static int sWeatherIntensity = 0;
    private static long sUpdateTime;

    public static void Init()
    {
        // Initialize with no weather by default
        sWeatherAnimationId = Guid.Empty;
        sWeatherXSpeed = 0;
        sWeatherYSpeed = 0;
        sWeatherIntensity = 0;
        sUpdateTime = 0;
    }

    public static void Update()
    {
        // For now, weather is static, but this method can be expanded
        // to implement dynamic weather changes based on time, events, etc.
        if (Timing.Global.Milliseconds > sUpdateTime)
        {
            // Update every 5 seconds if needed
            sUpdateTime = Timing.Global.Milliseconds + 5000;
            
            // Here you could add logic to change weather based on time, season, etc.
            // For example:
            // if (Time.GetTime().Hour >= 18 || Time.GetTime().Hour < 6)
            // {
            //     SetWeather(rainAnimationId, 2, 3, 50);
            // }
        }
    }

    public static void SetWeather(Guid animationId, int xSpeed, int ySpeed, int intensity)
    {
        sWeatherAnimationId = animationId;
        sWeatherXSpeed = xSpeed;
        sWeatherYSpeed = ySpeed;
        sWeatherIntensity = intensity;

        // Notify all clients
        PacketSender.SendGlobalWeatherToAll();
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
}
