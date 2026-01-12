using Newtonsoft.Json;

namespace Intersect.Server.Discord;

/// <summary>
/// Configuração da integração Discord
/// </summary>
public class DiscordConfig
{
    [JsonProperty("Enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("BotToken")]
    public string BotToken { get; set; } = string.Empty;

    [JsonProperty("GuildId")]
    public ulong GuildId { get; set; }

    [JsonProperty("Channels")]
    public DiscordChannels Channels { get; set; } = new();

    [JsonProperty("Features")]
    public DiscordFeatures Features { get; set; } = new();

    public static DiscordConfig Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            var defaultConfig = new DiscordConfig();
            Save(defaultConfig, filePath);
            return defaultConfig;
        }

        var json = File.ReadAllText(filePath);
        var config = JsonConvert.DeserializeObject<RootConfig>(json);
        return config?.Discord ?? new DiscordConfig();
    }

    public static void Save(DiscordConfig config, string filePath)
    {
        var rootConfig = new RootConfig { Discord = config };
        var json = JsonConvert.SerializeObject(rootConfig, Formatting.Indented);
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, json);
    }

    private class RootConfig
    {
        [JsonProperty("Discord")]
        public DiscordConfig Discord { get; set; } = new();
    }
}

public class DiscordChannels
{
    [JsonProperty("LogChannelId")]
    public ulong LogChannelId { get; set; }

    [JsonProperty("ChatChannelId")]
    public ulong ChatChannelId { get; set; }

    [JsonProperty("DropChannelId")]
    public ulong DropChannelId { get; set; }

    [JsonProperty("TradeChannelId")]
    public ulong TradeChannelId { get; set; }

    [JsonProperty("AdminChannelId")]
    public ulong AdminChannelId { get; set; }

    [JsonProperty("PlayerJoinChannelId")]
    public ulong PlayerJoinChannelId { get; set; }

    [JsonProperty("PlayerLeaveChannelId")]
    public ulong PlayerLeaveChannelId { get; set; }

    [JsonProperty("PlayerDeathChannelId")]
    public ulong PlayerDeathChannelId { get; set; }

    [JsonProperty("LevelUpChannelId")]
    public ulong LevelUpChannelId { get; set; }

    [JsonProperty("VerifiedRoleId")]
    public ulong VerifiedRoleId { get; set; } = 0; // Adicione o ID do cargo de verificado aqui
}

public class DiscordFeatures
{
    [JsonProperty("LogChat")]
    public bool LogChat { get; set; } = true;

    [JsonProperty("LogDrops")]
    public bool LogDrops { get; set; } = true;

    [JsonProperty("LogTrades")]
    public bool LogTrades { get; set; } = true;

    [JsonProperty("LogPlayerJoin")]
    public bool LogPlayerJoin { get; set; } = true;

    [JsonProperty("LogPlayerLeave")]
    public bool LogPlayerLeave { get; set; } = true;

    [JsonProperty("LogPlayerDeath")]
    public bool LogPlayerDeath { get; set; } = true;

    [JsonProperty("LogLevelUp")]
    public bool LogLevelUp { get; set; } = true;
}
