using System.Text;
using System.Globalization;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Intersect.Client.General;
using Intersect.Configuration;
using Intersect.Core;
using Microsoft.Extensions.Logging;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Framework.Threading;
using System.IO; // Added for file caching

namespace Intersect.Client.Localization;

public class TranslationService
{
    private static TranslationService _instance;
    public static TranslationService Instance => _instance ??= new TranslationService();

    // Configuration - In a real app, move these to ClientConfiguration
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions"; 
    private const string Model = "gpt-4o-mini"; // Fastest and most cost-effective model currently

    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, string> _translationCache;
    private readonly ConcurrentDictionary<string, string> _translationKeyCache; // Cache by ID/Key
    private readonly string _targetLanguage;
    private readonly string _cacheFilePath; // Cache file path
    private bool _enabled;
    
    // Batching Configuration
    private const int BATCH_SIZE = 20; // Number of strings per request

    private TranslationService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30); // OpenAI is fast, 30s is usually enough even for batches
        _translationCache = new ConcurrentDictionary<string, string>(); // Text -> Translated
        _translationKeyCache = new ConcurrentDictionary<string, string>(); // Key -> Translated
        
        // Detect System Language
        var currentCulture = CultureInfo.CurrentUICulture;
        _targetLanguage = currentCulture.DisplayName; 
        
        // Define cache path
        // Ensure the directory exists
        var cacheDir = Path.Combine("resources", "localization", "cache");
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }
        
        // Sanitize filename to avoid invalid characters
        var sanitizedLang = string.Join("_", _targetLanguage.Split(Path.GetInvalidFileNameChars()));
        _cacheFilePath = Path.Combine(cacheDir, $"{sanitizedLang}.json");

        // Load existing cache
        LoadCache();

        // Simple logic: if not English, enable translation. Adjust as needed.
        _enabled = !currentCulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);
    }

    public static void Init()
    {
        if (Instance._enabled)
        {
            // Start background translation of UI strings
            Task.Run(TranslateInterface);
            
            // Start background translation of Game Content (Items, Quests, etc.)
            // Removed fixed delay task. Replaced by trigger in PacketHandler.
        }
    }

    public async Task<string> Translate(string text)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(text)) return text;
        if (text.Length < 2) return text; 

        if (_translationCache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        // Use key from Options, which comes from Server or Local Config
        var apiKey = Options.Instance?.TranslationApiKey;

        // If no API Key is available, we cannot translate (and we missed the cache above).
        // Just return the original text.
        if (string.IsNullOrEmpty(apiKey))
        {
             return text;
        }

        try
        {
            // Fallback to single if called directly
            var translated = await RequestTranslationSingle(text);
            _translationCache[text] = translated;
            SaveCache(); // Save after new translation
            return translated;
        }
        catch (Exception ex)
        {
            ApplicationContext.Context.Value?.Logger.LogWarning($"Translation failed for '{text}': {ex.Message}");
            return text;
        }
    }
    
    public async Task<Dictionary<string, string>> TranslateBatch(Dictionary<string, string> inputs, Action<Dictionary<string, string>>? onChunkComplete = null)
    {
        var results = new Dictionary<string, string>();
        if (!_enabled || inputs.Count == 0) return results;

        var uncached = new Dictionary<string, string>();
        var cachedResults = new Dictionary<string, string>();

        // 1. Check Cache
        foreach (var kvp in inputs)
        {
            // First check Key Cache (ID/Key based) - Priority for Game Content
            if (_translationKeyCache.TryGetValue(kvp.Key, out var keyCached))
            {
                results[kvp.Key] = keyCached;
                cachedResults[kvp.Key] = keyCached;
            }
            // Fallback to Text Cache (Value based) - Optimization for UI
            else if (_translationCache.TryGetValue(kvp.Value, out var textCached))
            {
                results[kvp.Key] = textCached;
                cachedResults[kvp.Key] = textCached;
                
                // Promote to key cache for future stability
                _translationKeyCache.TryAdd(kvp.Key, textCached); 
            }
            else
            {
                uncached.Add(kvp.Key, kvp.Value);
            }
        }

        // Notify about cached immediately
        if (cachedResults.Count > 0)
        {
            onChunkComplete?.Invoke(cachedResults);
        }

        if (uncached.Count == 0) return results;
        
        // Use key from Options
        var apiKey = Options.Instance?.TranslationApiKey;

        // If no API Key is available, do not process uncached strings
        if (string.IsNullOrEmpty(apiKey))
        {
             return results;
        }

        ApplicationContext.Context.Value?.Logger.LogInformation($"Starting translation of {uncached.Count} strings to {_targetLanguage}...");

        // 2. Process Uncached in Chunks
        var chunks = uncached.Select(x => x).Chunk(BATCH_SIZE);
        
        foreach (var chunk in chunks)
        {
            var chunkDict = chunk.ToDictionary(k => k.Key, v => v.Value);
            var chunkResults = new Dictionary<string, string>();

            try 
            {
                var translatedChunk = await RequestTranslationBatch(chunkDict);
                
                foreach(var kvp in translatedChunk)
                {
                    results[kvp.Key] = kvp.Value;
                    chunkResults[kvp.Key] = kvp.Value;
                    
                    // Update Cache (Value -> Translated)
                    if (chunkDict.TryGetValue(kvp.Key, out var originalText))
                    {
                        _translationCache[originalText] = kvp.Value;
                        _translationKeyCache[kvp.Key] = kvp.Value; // Cache by Key as well
                    }
                }

                // Save cache after each batch to persist progress immediately
                SaveCache(); 
                
                ApplicationContext.Context.Value?.Logger.LogInformation($"Translated batch of {chunkDict.Count} strings.");
                
                // Notify progress
                if (chunkResults.Count > 0)
                {
                    onChunkComplete?.Invoke(chunkResults);
                }
            }
            catch (Exception ex)
            {
                ApplicationContext.Context.Value?.Logger.LogError($"Failed to translate batch: {ex.Message}");
            }
        }
        
        return results;
    }

    private void LoadCache()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var json = File.ReadAllText(_cacheFilePath, Encoding.UTF8);
                var loadedCache = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (loadedCache != null)
                {
                    foreach (var kvp in loadedCache)
                    {
                        // Identify if it's a GUID/Key or Text based on content or context (Simplified assumption)
                        // If key contains _, it's likely a Game Content Key (ITEM_NAME_...) or UI Key (MainMenu.Login)
                        // This heuristic might be weak, but for now we load into both or distinct?
                        
                        // Current Strategy: Load everything into Key cache. 
                        // If it's pure text cache, the key was the text.
                        
                        // We will populate both caches to support legacy/text-based and new key-based.
                        
                        _translationKeyCache.TryAdd(kvp.Key, kvp.Value);
                        
                        // If the key strictly doesn't look like an internal ID (no _ or .), assume it's source text? 
                        // Or just blindly add to text cache if it's not a known prefix?
                        // Safe bet: Don't pollute text cache with Keys.
                        
                        bool isStructureKey = kvp.Key.Contains('.') || kvp.Key.Contains('_');
                        if (!isStructureKey) 
                        {
                             _translationCache.TryAdd(kvp.Key, kvp.Value);
                        }
                    }
                    ApplicationContext.Context.Value?.Logger.LogInformation($"Loaded {_translationKeyCache.Count} translations from cache.");
                }
            }
        }
        catch (Exception ex)
        {
            ApplicationContext.Context.Value?.Logger.LogError($"Failed to load translation cache: {ex.Message}");
        }
    }

    private void SaveCache()
    {
        try
        {
            // We want to save primarily the KEY based cache, as it is more specific.
            // However, to support UI strings that rely on text matching (if any), we might need those too.
            // But TranslateBatch populates _translationKeyCache for UI strings too (MainMenu.Login).
            // So saving _translationKeyCache should be sufficient for everything handled by TranslateBatch.
            
            var json = JsonConvert.SerializeObject(_translationKeyCache, Formatting.Indented);
            File.WriteAllText(_cacheFilePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            ApplicationContext.Context.Value?.Logger.LogError($"Failed to save translation cache: {ex.Message}");
        }
    }

    private async Task<string> RequestTranslationSingle(string text)
    {
        // ... (Keep existing logic if needed for single calls) ...
        // Re-using batch logic logic for simplicity could be better, but keeping single prompt simple
        var prompt = $"Translate to {_targetLanguage}. Return ONLY translated text. Text: {text}";
        return await SendLlmRequest(prompt);
    }
    
    private async Task<Dictionary<string, string>> RequestTranslationBatch(Dictionary<string, string> texts)
    {
        // Construct JSON for the LLM to translate values
        var jsonPayload = JsonConvert.SerializeObject(texts);
        var prompt = $"You are a localization system. Translate the VALUES of the following JSON object to {_targetLanguage}. \n" + 
                     $"Do NOT translate keys. Do NOT add explanations. Return ONLY the valid JSON object.\n" + 
                     $"Preserve formatting tokens ({{0}}, \\c{{...}}).\n\n" +
                     $"JSON:\n{jsonPayload}";

        var responseText = await SendLlmRequest(prompt);
        
        // Clean up response if LLM adds markdown blocks
        responseText = responseText.Replace("```json", "").Replace("```", "").Trim();

        try 
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
             // If JSON parsing fails, log and return empty (or try repair)
             ApplicationContext.Context.Value?.Logger.LogWarning("LLM returned invalid JSON for batch.");
             return new Dictionary<string, string>();
        }
    }

    private async Task<string> SendLlmRequest(string prompt)
    {
        var requestBody = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = "You are a professional game localization assistant. Be concise." },
                new { role = "user", content = prompt }
            },
            temperature = 0.1, // Lower temperature for more deterministic/consistent JSON
            max_tokens = 2048
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var apiKey = Options.Instance?.TranslationApiKey;

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization") && !string.IsNullOrEmpty(apiKey))
        {
             _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        var response = await _httpClient.PostAsync(ApiUrl, content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        dynamic result = JsonConvert.DeserializeObject(responseString);
        return result.choices[0].message.content;
    }

    private static async Task TranslateInterface()
    {
        await Strings.TranslateAll(Instance);
    }

    public static async Task TranslateGameContent()
    {
        // Give the game a moment to load loaded descriptors from server/cache
        // Wait a short delay to ensure deserialization is fully complete if needed, but primarily triggered by GameData
        await Task.Delay(TimeSpan.FromSeconds(2));

        var inputs = new Dictionary<string, string>();

        // 1. Collect Items
        if (ItemDescriptor.Lookup != null)
        {
            foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>())
            {
                if (!string.IsNullOrWhiteSpace(item.Name)) inputs[$"ITEM_NAME_{item.Id}"] = item.Name;
                if (!string.IsNullOrWhiteSpace(item.Description)) inputs[$"ITEM_DESC_{item.Id}"] = item.Description;
            }
        }

        // 2. Collect Quests
        if (QuestDescriptor.Lookup != null)
        {
            foreach (var quest in QuestDescriptor.Lookup.Values.OfType<QuestDescriptor>())
            {
                if (!string.IsNullOrWhiteSpace(quest.Name)) inputs[$"QUEST_NAME_{quest.Id}"] = quest.Name;
                if (!string.IsNullOrWhiteSpace(quest.StartDescription)) inputs[$"QUEST_START_{quest.Id}"] = quest.StartDescription;
                if (!string.IsNullOrWhiteSpace(quest.BeforeDescription)) inputs[$"QUEST_BEFORE_{quest.Id}"] = quest.BeforeDescription;
                if (!string.IsNullOrWhiteSpace(quest.InProgressDescription)) inputs[$"QUEST_INPROG_{quest.Id}"] = quest.InProgressDescription;
                if (!string.IsNullOrWhiteSpace(quest.EndDescription)) inputs[$"QUEST_END_{quest.Id}"] = quest.EndDescription;

                if (quest.Tasks != null)
                {
                    foreach (var task in quest.Tasks)
                    {
                        if (!string.IsNullOrWhiteSpace(task.Description)) inputs[$"QUEST_TASK_{quest.Id}_{task.Id}"] = task.Description;
                    }
                }
            }
        }

        // 3. Collect Spells
        if (SpellDescriptor.Lookup != null)
        {
            foreach (var spell in SpellDescriptor.Lookup.Values.OfType<SpellDescriptor>())
            {
                if (!string.IsNullOrWhiteSpace(spell.Name)) inputs[$"SPELL_NAME_{spell.Id}"] = spell.Name;
                if (!string.IsNullOrWhiteSpace(spell.Description)) inputs[$"SPELL_DESC_{spell.Id}"] = spell.Description;
            }
        }

        if (inputs.Count > 0)
        {
            ApplicationContext.Context.Value?.Logger.LogInformation($"Queuing {inputs.Count} game strings for translation...");
        }

        // Send to Batch Translator
        await Instance.TranslateBatch(inputs, (chunkResults) =>
        {
            // Execute updates on the Main Thread to prevent deadlocks/UI thread collisions
            ThreadQueue.Default.RunOnMainThread(() => 
            {
                foreach (var kvp in chunkResults)
                {
                    var key = kvp.Key;
                    var translatedText = kvp.Value;
                    var parts = key.Split('_');
                    
                    if (parts.Length < 3) continue;

                    var type = parts[0];
                    var field = parts[1];
                    
                    if (!Guid.TryParse(parts[2], out var id)) continue;

                    if (type == "ITEM")
                    {
                        if (ItemDescriptor.TryGet(id, out var item))
                        {
                            if (field == "NAME") item.Name = translatedText;
                            else if (field == "DESC") item.Description = translatedText;
                        }
                    }
                    else if (type == "QUEST")
                    {
                        if (QuestDescriptor.TryGet(id, out var quest))
                        {
                            // Important: Ensure we are updating the actual descriptor properties
                            // and that these changes persist in memory for the UI to read.
                            if (field == "NAME") quest.Name = translatedText;
                            else if (field == "START") quest.StartDescription = translatedText;
                            else if (field == "BEFORE") quest.BeforeDescription = translatedText;
                            else if (field == "INPROG") quest.InProgressDescription = translatedText;
                            else if (field == "END") quest.EndDescription = translatedText;
                            else if (field == "TASK" && parts.Length >= 4 && Guid.TryParse(parts[3], out var taskId))
                            {
                                var task = quest.FindTask(taskId);
                                if (task != null) task.Description = translatedText;
                            }
                        }
                    }
                    else if (type == "SPELL")
                    {
                        if (SpellDescriptor.TryGet(id, out var spell))
                        {
                            if (field == "NAME") spell.Name = translatedText;
                            else if (field == "DESC") spell.Description = translatedText;
                        }
                    }
                }

                // Force refresh of quest log if open and showing this quest
                // This logic is a bit UI specific for this service, but necessary
                if (Intersect.Client.Interface.Interface.HasInGameUI)
                {
                        Intersect.Client.Interface.Interface.GameUi.NotifyQuestsUpdated();
                }
            });
        });
    }
}
