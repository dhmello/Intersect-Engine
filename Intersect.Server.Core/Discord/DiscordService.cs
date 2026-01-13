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
using Intersect.Enums;

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
    private readonly Timer _syncTimer;
    private bool _isRunning;
    private ulong _guildId;
    private ulong _botId;

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
    private ulong _verifiedRoleId;
    private ulong _unverifiedRoleId;

    public DiscordService(ILogger logger)
    {
        _logger = logger;
        _logQueue = new ConcurrentQueue<DiscordLogEntry>();
        
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | 
                           GatewayIntents.GuildMessages | 
                           GatewayIntents.MessageContent |
                           GatewayIntents.GuildMembers,
            LogLevel = LogSeverity.Info
        };

        _client = new DiscordSocketClient(config);
        
        // Eventos do Discord
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.UserJoined += UserJoinedAsync;

        // Timer para flush de logs (a cada 5 segundos)
        _logFlushTimer = new Timer(FlushLogs, null, 5000, 5000);

        // Timer para sincronização de roles (a cada 2 minutos e 30 segundos)
        _syncTimer = new Timer(SyncRoles, null, 150000, 150000);
    }

    /// <summary>
    /// Inicia o serviço Discord
    /// </summary>
    public async Task StartAsync(string token, ulong logChannelId, ulong chatChannelId, 
        ulong dropChannelId, ulong tradeChannelId, ulong adminChannelId,
        ulong playerJoinChannelId, ulong playerLeaveChannelId, 
        ulong playerDeathChannelId, ulong levelUpChannelId, ulong verifiedRoleId, ulong unverifiedRoleId, ulong guildId)
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
        _verifiedRoleId = verifiedRoleId;
        _unverifiedRoleId = unverifiedRoleId;
        _guildId = guildId;

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
    public void LogChat(string playerName, string message, Enums.ChatMessageType messageType)
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
        _botId = _client.CurrentUser.Id;
        _logger.LogInformation($"Discord Bot logado como {_client.CurrentUser} ({_botId})");

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
        
        // Executar sincronização inicial de roles após 10 segundos
        _ = Task.Run(async () =>
        {
            await Task.Delay(10000);
            await SyncRolesAsync();
        });
    }

    private async Task UserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            // Skip bots
            if (user.IsBot || user.Id == _botId || user.Id == 1460349640531640484)
            {
                return;
            }

            // Check if user is already linked
            var links = DiscordLinkManager.Instance.GetAllLinks();
            var isLinked = links.Values.Contains(user.Id);

            if (isLinked)
            {
                // Give verified role
                if (_verifiedRoleId != 0)
                {
                    var role = user.Guild.GetRole(_verifiedRoleId);
                    if (role != null)
                    {
                        await user.AddRoleAsync(role);
                        _logger.LogInformation($"✅ New member {user.Username} ({user.Id}) joined and received verified role (already linked)");
                    }
                }
            }
            else
            {
                // Give unverified role
                if (_unverifiedRoleId != 0)
                {
                    var role = user.Guild.GetRole(_unverifiedRoleId);
                    if (role != null)
                    {
                        await user.AddRoleAsync(role);
                        _logger.LogInformation($"⚠️ New member {user.Username} ({user.Id}) joined and received unverified role");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling user join for {user.Username} ({user.Id})");
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

        // Comando: /discord [codigo] - Vincula conta
        var discordCommand = new SlashCommandBuilder()
            .WithName("discord")
            .WithDescription("Vincula sua conta do jogo com o Discord")
            .AddOption("codigo", ApplicationCommandOptionType.String, "O código de 6 dígitos gerado no jogo (/discord)", isRequired: true)
            .Build();

        try
        {
            if (_guildId != 0)
            {
                var guild = _client.GetGuild(_guildId);
                if (guild != null)
                {
                    await guild.CreateApplicationCommandAsync(onlineCommand);
                    await guild.CreateApplicationCommandAsync(giveItemCommand);
                    await guild.CreateApplicationCommandAsync(setVariableCommand);
                    await guild.CreateApplicationCommandAsync(kickCommand);
                    await guild.CreateApplicationCommandAsync(announceCommand);
                    await guild.CreateApplicationCommandAsync(discordCommand);

                    _logger.LogInformation("Comandos slash registrados na guild com sucesso");
                }
                else
                {
                    _logger.LogError("Guild não encontrada para registrar comandos");
                }
            }
            else
            {
                await _client.CreateGlobalApplicationCommandAsync(onlineCommand);
                await _client.CreateGlobalApplicationCommandAsync(giveItemCommand);
                await _client.CreateGlobalApplicationCommandAsync(setVariableCommand);
                await _client.CreateGlobalApplicationCommandAsync(kickCommand);
                await _client.CreateGlobalApplicationCommandAsync(announceCommand);
                await _client.CreateGlobalApplicationCommandAsync(discordCommand);

                _logger.LogInformation("Comandos slash registrados globalmente com sucesso");
            }
        }
        catch (HttpException ex)
        {
            var json = JsonConvert.SerializeObject(ex.Errors, Formatting.Indented);
            _logger.LogError(ex, $"Erro ao registrar comandos: {json}");
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        try
        {
            switch (command.Data.Name)
            {
                case "online":
                case "giveitem":
                case "setvariable":
                case "kick":
                case "announce":
                    // Verificar se o comando é no canal admin
                    if (command.Channel.Id != _adminChannelId)
                    {
                        await command.RespondAsync("⛔ Este comando só pode ser usado no canal admin!", ephemeral: true);
                        return;
                    }
                    break;
                case "discord":
                    // Pode ser usado em qualquer canal
                    break;
            }

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
                case "discord":
                    await HandleDiscordCommand(command);
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

    private async Task HandleDiscordCommand(SocketSlashCommand command)
    {
        var code = command.Data.Options.First(o => o.Name == "codigo").Value.ToString();
        var discordUser = command.User;

        if (DiscordLinkManager.Instance.VerifyCode(code, discordUser.Id, out var gameUserId))
        {
            await command.RespondAsync("✅ Conta vinculada com sucesso! Você agora tem o cargo de verificado.", ephemeral: true);

            // Give verified role and remove unverified role
            try
            {
                if (command.Channel is SocketGuildChannel guildChannel)
                {
                    var user = guildChannel.Guild.GetUser(discordUser.Id);
                    
                    // Add verified role
                    if (_verifiedRoleId != 0 && user != null)
                    {
                        var verifiedRole = guildChannel.Guild.GetRole(_verifiedRoleId);
                        if (verifiedRole != null && !user.Roles.Contains(verifiedRole))
                        {
                            await user.AddRoleAsync(verifiedRole);
                            _logger.LogInformation($"✅ Added verified role to {user.Username} ({user.Id}) after linking");
                        }
                    }
                    
                    // Remove unverified role
                    if (_unverifiedRoleId != 0 && user != null)
                    {
                        var unverifiedRole = guildChannel.Guild.GetRole(_unverifiedRoleId);
                        if (unverifiedRole != null && user.Roles.Contains(unverifiedRole))
                        {
                            await user.RemoveRoleAsync(unverifiedRole);
                            _logger.LogInformation($"🗑️ Removed unverified role from {user.Username} ({user.Id}) after linking");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update roles after linking.");
            }
        }
        else
        {
            await command.RespondAsync("❌ Código inválido ou expirado. Digite `/discord` no jogo novamente para gerar um novo código.", ephemeral: true);
        }
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

    private async void SyncRoles(object? state)
    {
        await SyncRolesAsync();
    }

    private async Task SyncRolesAsync()
    {
        if (!_isRunning || _guildId == 0)
        {
            return;
        }

        try
        {
            var guild = _client.GetGuild(_guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild not found for role sync");
                return;
            }

            // Download all guild members if not already cached
            _logger.LogInformation("Downloading guild members...");
            await guild.DownloadUsersAsync();
            _logger.LogInformation($"Guild members downloaded. Total: {guild.MemberCount}");

            var links = DiscordLinkManager.Instance.GetAllLinks();
            var linkedDiscordIds = new HashSet<ulong>(links.Values);

            _logger.LogInformation($"Starting role sync. Guild has {guild.Users.Count} cached members, {guild.MemberCount} total members, {linkedDiscordIds.Count} linked accounts");

            int verifiedCount = 0;
            int unverifiedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            foreach (var member in guild.Users)
            {
                try
                {
                    // Skip bots
                    if (member.IsBot || member.Id == _botId || member.Id == 1460349640531640484)
                    {
                        skippedCount++;
                        continue;
                    }

                    bool isLinked = linkedDiscordIds.Contains(member.Id);
                    bool hasVerifiedRole = _verifiedRoleId != 0 && member.Roles.Any(r => r.Id == _verifiedRoleId);
                    bool hasUnverifiedRole = _unverifiedRoleId != 0 && member.Roles.Any(r => r.Id == _unverifiedRoleId);

                    if (isLinked)
                    {
                        // User is linked - should have verified role and not have unverified role
                        if (_verifiedRoleId != 0 && !hasVerifiedRole)
                        {
                            var role = guild.GetRole(_verifiedRoleId);
                            if (role != null)
                            {
                                await member.AddRoleAsync(role);
                                _logger.LogInformation($"✅ Added verified role to {member.Username} ({member.Id})");
                                verifiedCount++;
                            }
                        }
                        
                        if (_unverifiedRoleId != 0 && hasUnverifiedRole)
                        {
                            var role = guild.GetRole(_unverifiedRoleId);
                            if (role != null)
                            {
                                await member.RemoveRoleAsync(role);
                                _logger.LogInformation($"🗑️ Removed unverified role from {member.Username} ({member.Id})");
                            }
                        }
                    }
                    else
                    {
                        // User is NOT linked - should have unverified role and not have verified role
                        if (_unverifiedRoleId != 0 && !hasUnverifiedRole)
                        {
                            var role = guild.GetRole(_unverifiedRoleId);
                            if (role != null)
                            {
                                await member.AddRoleAsync(role);
                                _logger.LogInformation($"⚠️ Added unverified role to {member.Username} ({member.Id})");
                                unverifiedCount++;
                            }
                        }
                        
                        if (_verifiedRoleId != 0 && hasVerifiedRole)
                        {
                            var role = guild.GetRole(_verifiedRoleId);
                            if (role != null)
                            {
                                await member.RemoveRoleAsync(role);
                                _logger.LogInformation($"🗑️ Removed verified role from {member.Username} ({member.Id})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to sync role for Discord user {member.Username} ({member.Id})");
                    errorCount++;
                }
            }

            _logger.LogInformation($"Role sync completed: {verifiedCount} verified, {unverifiedCount} unverified, {skippedCount} skipped, {errorCount} errors");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync roles");
        }
    }

    private DiscordColor GetColorForMessageType(Enums.ChatMessageType messageType)
    {
        return messageType switch
        {
            Enums.ChatMessageType.Local => DiscordColor.Green,
            Enums.ChatMessageType.Global => DiscordColor.Blue,
            Enums.ChatMessageType.Party => new DiscordColor(128, 0, 128), // Purple
            Enums.ChatMessageType.Guild => DiscordColor.Orange,
            Enums.ChatMessageType.PM => new DiscordColor(0, 128, 128), // Teal
            Enums.ChatMessageType.Admin => DiscordColor.Red,
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
        _syncTimer?.Dispose();
        _client?.Dispose();
    }

    private class DiscordLogEntry
    {
        public ulong ChannelId { get; set; }
        public Embed Embed { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}