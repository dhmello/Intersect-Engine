using Intersect.Server.Core.CommandParsing;
using Intersect.Server.Core.CommandParsing.Arguments;
using Intersect.Server.General;
using Intersect.Server.Localization;
using Intersect.Server.Networking;

namespace Intersect.Server.Core.Commands;

internal sealed partial class WeatherCommand : ServerCommand
{
    public WeatherCommand() : base(
        Strings.Commands.Weather,
        new VariableArgument<string>(
            Strings.Commands.Arguments.WeatherAnimation,
            true,
            true
        ),
        new VariableArgument<int>(
            Strings.Commands.Arguments.WeatherXSpeed,
            false,
            true,
            defaultValue: 2
        ),
        new VariableArgument<int>(
            Strings.Commands.Arguments.WeatherYSpeed,
            false,
            true,
            defaultValue: 3
        ),
        new VariableArgument<int>(
            Strings.Commands.Arguments.WeatherIntensity,
            false,
            true,
            defaultValue: 50
        )
    )
    {
    }

    protected override void HandleValue(ServerContext context, ParserResult result)
    {
        // Find arguments by name instead of index to avoid HelpArgument conflict
        var animationArg = Arguments.FirstOrDefault(a => a.Name == Strings.Commands.Arguments.WeatherAnimation.Name) as CommandArgument<string>;
        var xSpeedArg = Arguments.FirstOrDefault(a => a.Name == Strings.Commands.Arguments.WeatherXSpeed.Name) as CommandArgument<int>;
        var ySpeedArg = Arguments.FirstOrDefault(a => a.Name == Strings.Commands.Arguments.WeatherYSpeed.Name) as CommandArgument<int>;
        var intensityArg = Arguments.FirstOrDefault(a => a.Name == Strings.Commands.Arguments.WeatherIntensity.Name) as CommandArgument<int>;

        var animationArgValue = result.Find(animationArg);
        var xSpeedArgValue = result.Find(xSpeedArg);
        var ySpeedArgValue = result.Find(ySpeedArg);
        var intensityArgValue = result.Find(intensityArg);

        // Check if clearing weather
        if (string.Equals(animationArgValue, "clear", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(animationArgValue, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(animationArgValue, "off", StringComparison.OrdinalIgnoreCase))
        {
            Weather.SetWeather(Guid.Empty, 0, 0, 0);
            Console.WriteLine("    Global weather cleared!");
            PacketSender.SendGlobalWeatherToAll();
            return;
        }

        // Try parse as GUID
        if (!Guid.TryParse(animationArgValue, out var animationId))
        {
            Console.WriteLine($"    Invalid animation ID: {animationArgValue}");
            Console.WriteLine("    Use a valid GUID or 'clear' to remove weather");
            Console.WriteLine($"    Example: weather {animationArgValue} {xSpeedArgValue} {ySpeedArgValue} {intensityArgValue}");
            return;
        }

        // Validate intensity
        var intensity = Math.Clamp(intensityArgValue, 0, 100);
        if (intensity != intensityArgValue)
        {
            Console.WriteLine($"    Intensity clamped to valid range (0-100): {intensity}");
        }

        // Set the weather (sound system removed)
        Weather.SetWeather(animationId, xSpeedArgValue, ySpeedArgValue, intensity);
        PacketSender.SendGlobalWeatherToAll();

        Console.WriteLine("    Global weather set!");
        Console.WriteLine($"    Animation ID: {animationId}");
        Console.WriteLine($"    X Speed: {xSpeedArgValue}");
        Console.WriteLine($"    Y Speed: {ySpeedArgValue}");
        Console.WriteLine($"    Intensity: {intensity}%");
    }
}
