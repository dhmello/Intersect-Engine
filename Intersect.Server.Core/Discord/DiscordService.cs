using Discord;
using Discord.Net;
using Discord.WebSocket;
using Intersect.Server.Core;
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

namespace Intersect.Server.Discord;

/// <summary>
/// Serviço de integração com Discord
/// Gerencia eventos do jogo e comandos do Discord
/// </summary>
public sealed class DiscordService : IDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<DiscordLogEntry> _logQueue;
    private readonly Timer _logFlushTimer;
    private bool _isRunning;

    // Configurações
    private string _botToken = string.Empty;
    private ulong _logChannelId;
    private ulong _chatChannelId;
    private ulong _dropChannelId;
    private ulong _tradeChannelId;
    private ulong _adminChannelId;
    private ulong _playerJoinChannelId;
    private ulong _playerLeaveChannelId;
    private ulong _playerDeathChannelId;
    private ulong _levelUpChannelId;

    public DiscordService(ILogger logger)
    {
        _logger = logger;
        _logQueue = new ConcurrentQueue<DiscordLogEntry>();
        
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | 
                           GatewayIntents.GuildMessages | 
                           GatewayIntents.MessageContent,
            LogLevel = LogSeverity.Info
        };

        _client = new DiscordSocketClient(config);
        
        // Eventos do Discord
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.SlashCommandExecuted += SlashCommandHandler;

        // Timer para flush de logs (a cada 5 segundos)
        _logFlushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Inicia o serviço Discord
    /// </summary>
    public async Task StartAsync(string token, ulong logChannelId, ulong chatChannelId, 
        ulong dropChannelId, ulong tradeChannelId, ulong adminChannelId,
        ulong playerJoinChannelId, ulong playerLeaveChannelId, 
        ulong playerDeathChannelId, ulong levelUpChannelId)
    {
        if (_isRunning)
        {
            return;
        }

        _botToken = token;
        _logChannelId = logChannelId;
        _chatChannelId = chatChannelId;
        _dropChannelId = dropChannelId;
        _tradeChannelId = tradeChannelId;
        _adminChannelId = adminChannelId;
        _playerJoinChannelId = playerJoinChannelId;
        _playerLeaveChannelId = playerLeaveChannelId;
        _playerDeathChannelId = playerDeathChannelId;
        _levelUpChannelId = levelUpChannelId;

        try
        {
            await _client.LoginAsync(TokenType.Bot, _botToken);
            await _client.StartAsync();
            _isRunning = true;
            
            _logger.LogInformation("Discord Bot conectado com sucesso!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao conectar Discord Bot");
            throw;
        }
    }

    /// <summary>
    /// Para o serviço Discord
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            await FlushLogsAsync();
            await _client.StopAsync();
            await _client.LogoutAsync();
            _isRunning = false;
            
            _logger.LogInformation("Discord Bot desconectado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desconectar Discord Bot");
        }
    }

    #region Eventos do Jogo

    /// <summary>
    /// Loga mensagem de chat do jogo
    /// </summary>
    public void LogChat(string playerName, string message, ChatMessageType messageType)
    {
        var embed = new EmbedBuilder()
            .WithTitle("💬 Chat")
            .WithColor(GetColorForMessageType(messageType))
            .WithDescription($"**{playerName}**: {message}")
            .WithCurrentTimestamp()
            .Build();

        EnqueueLog(_chatChannelId, embed);
    }

    /// <summary>
    /// Loga drop de item
    /// </summary>
    public void LogDrop(string playerName, string itemName, int quantity, string mapName)
    {
        var embed = new EmbedBuilder()
            .WithTitle("📦 Item Dropado")
            .WithColor(DiscordColor.Orange)
            .AddField("Jogador", playerName, inline: true)
            .AddField("Item", itemName, inline: true)
            .AddField("Quantidade", quantity.ToString(), inline: true)
            .AddField("Mapa", mapName, inline: false)
            .WithCurrentTimestamp()
            .Build();

        EnqueueLog(_dropChannelId, embed);
    }

    /// <summary>
    /// Loga trade entre jogadores
    /// </summary>
    public void LogTrade(string player1Name, string player2Name, 
        Dictionary<string, int> player1Items, Dictionary<string, int> player2Items)
    {
        var player1ItemsList = string.Join("\n", player1Items.Select(i => $"• {i.Key} x{i.Value}"));
        var player2ItemsList = string.Join("\n", player2Items.Select(i => $"• {i.Key} x{i.Value}"));

        var embed = new EmbedBuilder()
            .WithTitle("🤝 Trade Realizada")
            .WithColor(DiscordColor.Green)
            .AddField($"📤 {player1Name} enviou:", player1ItemsList.Length > 0 ? player1ItemsList : "Nada", inline: true)
            .AddField($"📥 {player2Name} enviou:", player2ItemsList.Length > 0 ? player2ItemsList : "Nada", inline: true)
            .WithCurrentTimestamp()
            .Build();

        EnqueueLog(_tradeChannelId, embed);
    }

    /// <summary>
    /// Loga entrada de jogador
    /// </summary>
    public void LogPlayerJoin(string playerName, string ip)
    {
        var embed = new EmbedBuilder()
            .WithTitle("✅ Jogador Conectou")
            .WithColor(DiscordColor.Green)
            .AddField("Nome", playerName, inline: true)
            .AddField("IP", ip, inline: true)
            .WithCurrentTimestamp()
            .Build();

        EnqueueLog(_playerJoinChannelId != 0 ? _playerJoinChannelId : _logChannelId, embed);
    }

    /// <summary>
    /// Loga saída de jogador
    /// </summary>
    public void LogPlayerLeave(string playerName, string reason)
    {
        var embed = new EmbedBuilder()
            .WithTitle("❌ Jogador Desconectou")
            .WithColor(DiscordColor.Red)
            .AddField("Nome", playerName, inline: true)
            .AddField("Motivo", reason, inline: true)
            .WithCurrentTimestamp()
            .Build();

        EnqueueLog(_playerLeaveChannelId != 0 ? _playerLeaveChannelId : _logChannelId, embed);
    }

    /// <summary>
    /// Loga morte de jogador
    /// </summary>
    public void LogPlayerDeath(string playerName, string killerName, string mapName)
    {
        var embed = new EmbedBuilder()
            .WithTitle("💀 Jogador Morreu")
            .WithColor(new DiscordColor(139, 0, 0)) // DarkRed
            .AddField("Vítima", playerName, inline: true)
            .AddField("Assassino", killerName, inline: true)
            .AddField("Mapa", mapName, inline: false)
            .WithCurrentTimestamp()
            .Build();

        EnqueueLog(_playerDeathChannelId != 0 ? _playerDeathChannelId : _logChannelId, embed);
    }

    /// <summary>
    /// Loga level up de jogador
    /// </summary>
    public void LogLevelUp(string playerName, int newLevel)
    {
        var embed = new EmbedBuilder()
            .WithTitle("⭐ Level Up!")
            .WithColor(new DiscordColor(255, 215, 0)) // Gold
            .AddField("Jogador", playerName, inline: true)
            .AddField("Novo Level", newLevel.ToString(), inline: true)
            .WithCurrentTimestamp()
            .Build();

        EnqueueLog(_levelUpChannelId != 0 ? _levelUpChannelId : _logChannelId, embed);
    }

    #endregion

    #region Comandos do Discord

    private async Task ReadyAsync()
    {
        _logger.LogInformation($"Discord Bot logado como {_client.CurrentUser}");

        // Renomear canais
        await RenameChannelsAsync();

        // Registrar comandos slash
        try
        {
            await RegisterSlashCommandsAsync();
        }
        catch (HttpException ex)
        {
            _logger.LogError(ex, "Erro ao registrar comandos slash");
        }
    }

    private async Task RenameChannelsAsync()
    {
        var channelMappings = new Dictionary<ulong, string>
        {
            { _chatChannelId, "💬・chat-global" },
            { _dropChannelId, "📦・drops" },
            { _tradeChannelId, "🤝・trades" },
            { _playerJoinChannelId, "🟢・entradas" },
            { _playerLeaveChannelId, "🔴・saidas" },
            { _playerDeathChannelId, "💀・mortes" },
            { _levelUpChannelId, "⭐・level-up" },
            { _adminChannelId, "🛡️・admin" }
        };

        foreach (var mapping in channelMappings)
        {
            if (mapping.Key == 0) continue;

            try
            {
                var channel = _client.GetChannel(mapping.Key) as SocketTextChannel;
                if (channel != null && channel.Name != mapping.Value)
                {
                    await channel.ModifyAsync(properties => properties.Name = mapping.Value);
                    _logger.LogInformation($"Canal {mapping.Key} renomeado para {mapping.Value}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao renomear canal {mapping.Key} para {mapping.Value}");
            }
        }
    }

    private async Task RegisterSlashCommandsAsync()
    {
        // Comando: /online - Lista jogadores online
        var onlineCommand = new SlashCommandBuilder()
            .WithName("online")
            .WithDescription("Lista jogadores online no servidor")
            .Build();

        // Comando: /giveitem - Dá item a um jogador
        var giveItemCommand = new SlashCommandBuilder()
            .WithName("giveitem")
            .WithDescription("Dá um item a um jogador")
            .AddOption("player", ApplicationCommandOptionType.String, "Nome do jogador", isRequired: true)
            .AddOption("item", ApplicationCommandOptionType.String, "Nome do item", isRequired: true)
            .AddOption("quantity", ApplicationCommandOptionType.Integer, "Quantidade", isRequired: true)
            .Build();

        // Comando: /setvariable - Define valor de variável
        var setVariableCommand = new SlashCommandBuilder()
            .WithName("setvariable")
            .WithDescription("Define valor de uma variável de jogador")
            .AddOption("player", ApplicationCommandOptionType.String, "Nome do jogador", isRequired: true)
            .AddOption("variable", ApplicationCommandOptionType.String, "Nome da variável", isRequired: true)
            .AddOption("value", ApplicationCommandOptionType.Integer, "Valor", isRequired: true)
            .Build();

        // Comando: /kick - Kicka um jogador
        var kickCommand = new SlashCommandBuilder()
            .WithName("kick")
            .WithDescription("Remove um jogador do servidor")
            .AddOption("player", ApplicationCommandOptionType.String, "Nome do jogador", isRequired: true)
            .AddOption("reason", ApplicationCommandOptionType.String, "Motivo", isRequired: false)
            .Build();

        // Comando: /announce - Anuncia mensagem no jogo
        var announceCommand = new SlashCommandBuilder()
            .WithName("announce")
            .WithDescription("Envia mensagem para todos os jogadores")
            .AddOption("message", ApplicationCommandOptionType.String, "Mensagem", isRequired: true)
            .Build();

        try
        {
            await _client.CreateGlobalApplicationCommandAsync(onlineCommand);
            await _client.CreateGlobalApplicationCommandAsync(giveItemCommand);
            await _client.CreateGlobalApplicationCommandAsync(setVariableCommand);
            await _client.CreateGlobalApplicationCommandAsync(kickCommand);
            await _client.CreateGlobalApplicationCommandAsync(announceCommand);

            _logger.LogInformation("Comandos slash registrados com sucesso");
        }
        catch (HttpException ex)
        {
            var json = JsonConvert.SerializeObject(ex.Errors, Formatting.Indented);
            _logger.LogError(ex, $"Erro ao registrar comandos: {json}");
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        // Verificar se o comando é no canal admin
        if (command.Channel.Id != _adminChannelId)
        {
            await command.RespondAsync("⛔ Este comando só pode ser usado no canal admin!", ephemeral: true);
            return;
        }

        try
        {
            switch (command.Data.Name)
            {
                case "online":
                    await HandleOnlineCommand(command);
                    break;
                case "giveitem":
                    await HandleGiveItemCommand(command);
                    break;
                case "setvariable":
                    await HandleSetVariableCommand(command);
                    break;
                case "kick":
                    await HandleKickCommand(command);
                    break;
                case "announce":
                    await HandleAnnounceCommand(command);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao executar comando {command.Data.Name}");
            await command.RespondAsync($"❌ Erro ao executar comando: {ex.Message}", ephemeral: true);
        }
    }

    private async Task HandleOnlineCommand(SocketSlashCommand command)
    {
        var players = Client.Instances
            .Where(c => c?.Entity != null)
            .Select(c => c.Entity)
            .ToList();

        if (players.Count == 0)
        {
            await command.RespondAsync("📭 Nenhum jogador online no momento.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"🎮 Jogadores Online ({players.Count})")
            .WithColor(DiscordColor.Blue)
            .WithCurrentTimestamp();

        foreach (var player in players.Take(25)) // Discord limita a 25 fields
        {
            embed.AddField(
                player.Name,
                $"Level {player.Level} | Mapa: {player.MapName}",
                inline: true
            );
        }

        await command.RespondAsync(embed: embed.Build());
    }

    private async Task HandleGiveItemCommand(SocketSlashCommand command)
    {
        var playerName = command.Data.Options.First(o => o.Name == "player").Value.ToString();
        var itemName = command.Data.Options.First(o => o.Name == "item").Value.ToString();
        var quantity = Convert.ToInt32(command.Data.Options.First(o => o.Name == "quantity").Value);

        // TODO: Implementar via API REST do Intersect
        // Por enquanto, vamos simular
        await command.RespondAsync(
            $"✅ Comando enviado: Dar {quantity}x **{itemName}** para **{playerName}**\n" +
            $"⚠️ Implementar via API REST do servidor"
        );
    }

    private async Task HandleSetVariableCommand(SocketSlashCommand command)
    {
        var playerName = command.Data.Options.First(o => o.Name == "player").Value.ToString();
        var variableName = command.Data.Options.First(o => o.Name == "variable").Value.ToString();
        var value = Convert.ToInt32(command.Data.Options.First(o => o.Name == "value").Value);

        // TODO: Implementar via API REST do Intersect
        await command.RespondAsync(
            $"✅ Comando enviado: Definir variável **{variableName}** = **{value}** para **{playerName}**\n" +
            $"⚠️ Implementar via API REST do servidor"
        );
    }

    private async Task HandleKickCommand(SocketSlashCommand command)
    {
        var playerName = command.Data.Options.First(o => o.Name == "player").Value.ToString();
        var reason = command.Data.Options.FirstOrDefault(o => o.Name == "reason")?.Value?.ToString() ?? "Kickado por admin";

        var client = Client.Instances
            .FirstOrDefault(c => c?.Entity?.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase) ?? false);

        if (client == null)
        {
            await command.RespondAsync($"❌ Jogador **{playerName}** não encontrado online.");
            return;
        }

        client.Disconnect(reason);
        await command.RespondAsync($"✅ Jogador **{playerName}** foi kickado. Motivo: {reason}");
    }

    private async Task HandleAnnounceCommand(SocketSlashCommand command)
    {
        var message = command.Data.Options.First(o => o.Name == "message").Value.ToString();

        PacketSender.SendGlobalMsg(message!);
        
        await command.RespondAsync($"📢 Mensagem enviada para todos os jogadores:\n> {message}");
    }

    #endregion

    #region Helpers

    private void EnqueueLog(ulong channelId, Embed embed)
    {
        _logQueue.Enqueue(new DiscordLogEntry
        {
            ChannelId = channelId,
            Embed = embed,
            Timestamp = DateTime.UtcNow
        });
    }

    private async void FlushLogs(object? state)
    {
        await FlushLogsAsync();
    }

    private async Task FlushLogsAsync()
    {
        if (_logQueue.IsEmpty || !_isRunning)
        {
            return;
        }

        var batch = new List<DiscordLogEntry>();
        while (_logQueue.TryDequeue(out var entry) && batch.Count < 10)
        {
            batch.Add(entry);
        }

        foreach (var group in batch.GroupBy(e => e.ChannelId))
        {
            try
            {
                var channel = _client.GetChannel(group.Key) as IMessageChannel;
                if (channel == null)
                {
                    continue;
                }

                foreach (var entry in group)
                {
                    await channel.SendMessageAsync(embed: entry.Embed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao enviar logs para canal {group.Key}");
            }
        }
    }

    private DiscordColor GetColorForMessageType(ChatMessageType messageType)
    {
        return messageType switch
        {
            ChatMessageType.Global => DiscordColor.Blue,
            ChatMessageType.Local => DiscordColor.Green,
            ChatMessageType.Party => new DiscordColor(128, 0, 128), // Purple
            ChatMessageType.Guild => DiscordColor.Orange,
            ChatMessageType.Private => new DiscordColor(0, 128, 128), // Teal
            ChatMessageType.Admin => DiscordColor.Red,
            _ => new DiscordColor(211, 211, 211) // LightGray (Default)
        };
    }

    private Task LogAsync(LogMessage msg)
    {
        _logger.LogInformation($"Discord: {msg}");
        return Task.CompletedTask;
    }

    #endregion

    public void Dispose()
    {
        _logFlushTimer?.Dispose();
        _client?.Dispose();
    }

    private class DiscordLogEntry
    {
        public ulong ChannelId { get; set; }
        public Embed Embed { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}

public enum ChatMessageType
{
    Local,
    Global,
    Party,
    Guild,
    Private,
    Admin
}
