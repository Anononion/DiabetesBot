using System.Text.Json;

namespace DiabetesBot.Services;

public class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private readonly string _basePath;

    public LocalizationService()
    {
        _basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "users");
        LoadLanguage("ru");
        LoadLanguage("kk");
    }

    private void LoadLanguage(string lang)
    {
        var path = Path.Combine(_basePath, $"lang_{lang}.json");
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (dict != null)
            _translations[lang] = dict;
    }

    public string T(string lang, string key)
    {
        if (_translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        return key; // fallback
    }
}
