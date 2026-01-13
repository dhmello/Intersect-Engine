using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Intersect.Server.Core;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Discord;

public class DiscordLinkManager
{
    private static DiscordLinkManager? _instance;
    public static DiscordLinkManager Instance => _instance ??= new DiscordLinkManager();

    private readonly ConcurrentDictionary<string, (Guid UserId, DateTime Expiry)> _pendingCodes = new();
    private readonly string _dbPath;
    private ILogger _logger;

    private DiscordLinkManager()
    {
        _dbPath = Path.Combine(ServerContext.ResourceDirectory, "discord.db");
        InitializeDatabase();
    }

    public static void SetLogger(ILogger logger)
    {
        if (_instance != null)
        {
            _instance._logger = logger;
        }
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath};");
            connection.Open();

            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Links (
                    GameUserId TEXT PRIMARY KEY,
                    DiscordUserId INTEGER NOT NULL,
                    LinkedDate DATETIME NOT NULL
                )";

            using var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();

            Console.WriteLine($"Discord link database initialized at {_dbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Discord link database: {ex.Message}");
            _logger?.LogError(ex, "Failed to initialize Discord link database.");
        }
    }

    public string GenerateCode(Guid gameUserId)
    {
        // Remove existing codes for this user if any
        foreach (var kvp in _pendingCodes)
        {
            if (kvp.Value.UserId == gameUserId)
            {
                _pendingCodes.TryRemove(kvp.Key, out _);
            }
        }

        // Generate secure 6 digit code
        var code = Random.Shared.Next(100000, 999999).ToString();
        
        // Expires in 10 minutes
        _pendingCodes.TryAdd(code, (gameUserId, DateTime.UtcNow.AddMinutes(10)));

        return code;
    }

    public bool VerifyCode(string code, ulong discordUserId, out Guid gameUserId)
    {
        gameUserId = Guid.Empty;

        if (!_pendingCodes.TryGetValue(code, out var data))
        {
            return false;
        }

        if (DateTime.UtcNow > data.Expiry)
        {
            _pendingCodes.TryRemove(code, out _);
            return false;
        }

        _pendingCodes.TryRemove(code, out _);
        gameUserId = data.UserId;

        return SaveLink(gameUserId, discordUserId);
    }

    private bool SaveLink(Guid gameUserId, ulong discordUserId)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath};");
            connection.Open();

            string insertQuery = @"
                INSERT OR REPLACE INTO Links (GameUserId, DiscordUserId, LinkedDate)
                VALUES (@GameUserId, @DiscordUserId, @LinkedDate)";

            using var command = new SqliteCommand(insertQuery, connection);
            command.Parameters.AddWithValue("@GameUserId", gameUserId.ToString());
            command.Parameters.AddWithValue("@DiscordUserId", (long)discordUserId); // SQLite stores ulong as long usually, careful with sign
            command.Parameters.AddWithValue("@LinkedDate", DateTime.UtcNow);

            command.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save link between game user and Discord user.");
            return false;
        }
    }

    public bool IsLinked(Guid gameUserId)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath};");
            connection.Open();

            string query = "SELECT COUNT(1) FROM Links WHERE GameUserId = @GameUserId";
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@GameUserId", gameUserId.ToString());

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check if game user is linked to a Discord account.");
            return false;
        }
    }
    
    public bool GetDiscordId(Guid gameUserId, out ulong discordId)
    {
        discordId = 0;
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath};");
            connection.Open();

            string query = "SELECT DiscordUserId FROM Links WHERE GameUserId = @GameUserId";
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@GameUserId", gameUserId.ToString());

            var result = command.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                discordId = (ulong)(long)result;
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve Discord ID for the game user.");
            return false;
        }
    }

    public Dictionary<Guid, ulong> GetAllLinks()
    {
        var links = new Dictionary<Guid, ulong>();
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath};");
            connection.Open();

            string query = "SELECT GameUserId, DiscordUserId FROM Links";
            using var command = new SqliteCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var gameUserId = Guid.Parse(reader.GetString(0));
                var discordUserId = (ulong)(long)reader.GetInt64(1);
                links[gameUserId] = discordUserId;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve all Discord links.");
        }
        return links;
    }
}
