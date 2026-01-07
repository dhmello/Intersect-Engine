using System.Reflection;
using System.Collections;
using Intersect.Localization; // For LocalizedString

namespace Intersect.Client.Localization;

public static partial class Strings
{
    private static readonly HashSet<string> PriorityKeys = new()
    {
        "MainMenu.Login",
        "MainMenu.Register",
        "MainMenu.Settings",
        "MainMenu.Credits",
        "MainMenu.Exit",
        "MainMenu.Start",
        "MainMenu.Title",
        "LoginWindow.Title",
        "LoginWindow.Username",
        "LoginWindow.Password",
        "LoginWindow.Login",
        "LoginWindow.Back",
        "Registration.Title",
        "Registration.Username",
        "Registration.Password",
        "Registration.ConfirmPassword",
        "Registration.Email",
        "Registration.Register",
        "Registration.Back",
        "Settings.Title",
         "Credits.Title",
        "Credits.Back"
         // Add more specific sub-keys if needed
    };

    public static async Task TranslateAll(TranslationService service)
    {
        var rootType = typeof(Strings);
        var groupTypes = rootType.GetNestedTypes(BindingFlags.Static | BindingFlags.Public);
        
        var priorityItems = new Dictionary<string, string>();
        var otherItems = new Dictionary<string, string>();
        
        var applyActions = new Dictionary<string, Action<string>>();

        foreach (var groupType in groupTypes)
        {
            foreach (var fieldInfo in groupType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var fieldValue = fieldInfo.GetValue(null);

                if (fieldValue is LocalizedString localizedString)
                {
                    var original = localizedString.ToString();
                    if (string.IsNullOrWhiteSpace(original) || original.Length < 2) continue;

                    string key = $"{groupType.Name}.{fieldInfo.Name}";
                    var targetDict = PriorityKeys.Contains(key) ? priorityItems : otherItems;

                    if (!applyActions.ContainsKey(key))
                    {
                        targetDict.Add(key, original);
                        applyActions.Add(key, (translated) => {
                             fieldInfo.SetValue(null, new LocalizedString(translated));
                        });
                    }
                }
                else if (fieldValue is IDictionary dictionary)
                {
                    var keys = new ArrayList(dictionary.Keys); 
                    foreach (var dictKey in keys)
                    {
                        var val = dictionary[dictKey];
                        if (val is LocalizedString locVal)
                        {
                            var original = locVal.ToString();
                             if (string.IsNullOrWhiteSpace(original) || original.Length < 2) continue;

                            string key = $"{groupType.Name}.{fieldInfo.Name}[{dictKey}]";
                            
                            // Check if parent key implies priority (rare for dictionaries in main menu but possible)
                            var targetDict = PriorityKeys.Contains($"{groupType.Name}.{fieldInfo.Name}") ? priorityItems : otherItems;

                            if (!applyActions.ContainsKey(key))
                            {
                                targetDict.Add(key, original);
                                applyActions.Add(key, (translated) => {
                                     dictionary[dictKey] = new LocalizedString(translated);
                                });
                            }
                        }
                    }
                }
            }
        }

        // 1. Process Priority Items
        if (priorityItems.Count > 0)
        {
            await service.TranslateBatch(priorityItems, (results) => {
                ApplyTranslations(results, applyActions);
            });
        }

        // 2. Process Others
        if (otherItems.Count > 0)
        {
             await service.TranslateBatch(otherItems, (results) => {
                ApplyTranslations(results, applyActions);
            });
        }
    }

    private static void ApplyTranslations(Dictionary<string, string> translations, Dictionary<string, Action<string>> actions)
    {
        foreach (var kvp in translations)
        {
            if (actions.TryGetValue(kvp.Key, out var action))
            {
                action(kvp.Value);
            }
        }
        
        // Removed Strings.Save() to prevent overwriting client_strings.json.
        // TranslationService caches translations separately.
    }
}
