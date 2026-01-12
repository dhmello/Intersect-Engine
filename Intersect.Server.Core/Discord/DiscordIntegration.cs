using Intersect.Server.Discord;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.General;
using Intersect.Server.Localization;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using DiscordColor = Discord.Color;
using IntersectColor = Intersect.Color;
using Intersect.Enums;

namespace Intersect.Server.Discord;

/// <summary>
/// Gerenciador global da integração Discord
/// Fornece acesso ao serviço Discord em todo o servidor
/// </summary>
public static class DiscordIntegration
{
    private static DiscordService? _service;
    private static DiscordConfig? _config;
    private static bool _initialized;

    /// <summary>
    /// Serviço Discord ativo
    /// </summary>
    public static DiscordService? Service => _service;

    /// <summary>
    /// Configuração Discord ativa
    /// </summary>
    public static DiscordConfig? Config => _config;

    /// <summary>
    /// Verifica se o Discord está habilitado e funcionando
    /// </summary>
    public static bool IsEnabled => _initialized && _service != null && _config?.Enabled == true;

    /// <summary>
    /// Inicializa a integração Discord
    /// </summary>
    internal static void Initialize(DiscordService service, DiscordConfig config)
    {
        _service = service;
        _config = config;
        _initialized = true;
    }

    /// <summary>
    /// Loga mensagem de chat se habilitado
    /// </summary>
    public static void LogChat(string playerName, string message, ChatMessageType messageType)
    {
        if (!IsEnabled || !(_config?.Features.LogChat ?? false))
        {
            return;
        }

        _service?.LogChat(playerName, message, messageType);
    }

    /// <summary>
    /// Loga drop de item se habilitado
    /// </summary>
    public static void LogDrop(string playerName, string itemName, int quantity, string mapName)
    {
        if (!IsEnabled || !(_config?.Features.LogDrops ?? false))
        {
            return;
        }

        _service?.LogDrop(playerName, itemName, quantity, mapName);
    }

    /// <summary>
    /// Loga trade se habilitado
    /// </summary>
    public static void LogTrade(string player1Name, string player2Name,
        Dictionary<string, int> player1Items, Dictionary<string, int> player2Items)
    {
        if (!IsEnabled || !(_config?.Features.LogTrades ?? false))
        {
            return;
        }

        _service?.LogTrade(player1Name, player2Name, player1Items, player2Items);
    }

    /// <summary>
    /// Loga entrada de jogador se habilitado
    /// </summary>
    public static void LogPlayerJoin(string playerName, string ip)
    {
        if (!IsEnabled || !(_config?.Features.LogPlayerJoin ?? false))
        {
            return;
        }

        _service?.LogPlayerJoin(playerName, ip);
    }

    /// <summary>
    /// Loga saída de jogador se habilitado
    /// </summary>
    public static void LogPlayerLeave(string playerName, string reason)
    {
        if (!IsEnabled || !(_config?.Features.LogPlayerLeave ?? false))
        {
            return;
        }

        _service?.LogPlayerLeave(playerName, reason);
    }

    /// <summary>
    /// Loga morte de jogador se habilitado
    /// </summary>
    public static void LogPlayerDeath(string playerName, string killerName, string mapName)
    {
        if (!IsEnabled || !(_config?.Features.LogPlayerDeath ?? false))
        {
            return;
        }

        _service?.LogPlayerDeath(playerName, killerName, mapName);
    }

    /// <summary>
    /// Loga level up se habilitado
    /// </summary>
    public static void LogLevelUp(string playerName, int newLevel)
    {
        if (!IsEnabled || !(_config?.Features.LogLevelUp ?? false))
        {
            return;
        }

        _service?.LogLevelUp(playerName, newLevel);
    }

    /// <summary>
    /// Desliga o serviço Discord
    /// </summary>
    internal static async Task ShutdownAsync()
    {
        if (_service != null)
        {
            await _service.StopAsync();
            _service.Dispose();
            _service = null;
        }

        _initialized = false;
    }
}
