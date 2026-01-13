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
        var config = JsonConvert.DeserializeObject<RootConfig>(json)?.Discord ?? new DiscordConfig();

        // Ensure all default values are set if missing
        var defaults = new DiscordConfig();
        if (config.Enabled == default && defaults.Enabled != default) config.Enabled = defaults.Enabled;
        if (config.BotToken == default && defaults.BotToken != default) config.BotToken = defaults.BotToken;
        if (config.GuildId == default && defaults.GuildId != default) config.GuildId = defaults.GuildId;

        // Channels
        if (config.Channels == null) config.Channels = new DiscordChannels();
        if (config.Channels.LogChannelId == default && defaults.Channels.LogChannelId != default) config.Channels.LogChannelId = defaults.Channels.LogChannelId;
        if (config.Channels.ChatChannelId == default && defaults.Channels.ChatChannelId != default) config.Channels.ChatChannelId = defaults.Channels.ChatChannelId;
        if (config.Channels.DropChannelId == default && defaults.Channels.DropChannelId != default) config.Channels.DropChannelId = defaults.Channels.DropChannelId;
        if (config.Channels.TradeChannelId == default && defaults.Channels.TradeChannelId != default) config.Channels.TradeChannelId = defaults.Channels.TradeChannelId;
        if (config.Channels.AdminChannelId == default && defaults.Channels.AdminChannelId != default) config.Channels.AdminChannelId = defaults.Channels.AdminChannelId;
        if (config.Channels.PlayerJoinChannelId == default && defaults.Channels.PlayerJoinChannelId != default) config.Channels.PlayerJoinChannelId = defaults.Channels.PlayerJoinChannelId;
        if (config.Channels.PlayerLeaveChannelId == default && defaults.Channels.PlayerLeaveChannelId != default) config.Channels.PlayerLeaveChannelId = defaults.Channels.PlayerLeaveChannelId;
        if (config.Channels.PlayerDeathChannelId == default && defaults.Channels.PlayerDeathChannelId != default) config.Channels.PlayerDeathChannelId = defaults.Channels.PlayerDeathChannelId;
        if (config.Channels.LevelUpChannelId == default && defaults.Channels.LevelUpChannelId != default) config.Channels.LevelUpChannelId = defaults.Channels.LevelUpChannelId;
        if (config.Channels.VerifiedRoleId == default && defaults.Channels.VerifiedRoleId != default) config.Channels.VerifiedRoleId = defaults.Channels.VerifiedRoleId;
        if (config.Channels.UnverifiedRoleId == default && defaults.Channels.UnverifiedRoleId != default) config.Channels.UnverifiedRoleId = defaults.Channels.UnverifiedRoleId;

        // Features
        if (config.Features == null) config.Features = new DiscordFeatures();
        if (config.Features.LogChat == default && defaults.Features.LogChat != default) config.Features.LogChat = defaults.Features.LogChat;
        if (config.Features.LogDrops == default && defaults.Features.LogDrops != default) config.Features.LogDrops = defaults.Features.LogDrops;
        if (config.Features.LogTrades == default && defaults.Features.LogTrades != default) config.Features.LogTrades = defaults.Features.LogTrades;
        if (config.Features.LogPlayerJoin == default && defaults.Features.LogPlayerJoin != default) config.Features.LogPlayerJoin = defaults.Features.LogPlayerJoin;
        if (config.Features.LogPlayerLeave == default && defaults.Features.LogPlayerLeave != default) config.Features.LogPlayerLeave = defaults.Features.LogPlayerLeave;
        if (config.Features.LogPlayerDeath == default && defaults.Features.LogPlayerDeath != default) config.Features.LogPlayerDeath = defaults.Features.LogPlayerDeath;
        if (config.Features.LogLevelUp == default && defaults.Features.LogLevelUp != default) config.Features.LogLevelUp = defaults.Features.LogLevelUp;

        // Save the updated config back to file
        Save(config, filePath);

        return config;
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

    [JsonProperty("UnverifiedRoleId")]
    public ulong UnverifiedRoleId { get; set; } = 0; // Adicione o ID do cargo de não verificado aqui
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
