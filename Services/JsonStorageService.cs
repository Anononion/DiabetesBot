using System.Text.Json;
using DiabetesBot.Models;
using DiabetesBot.Utils.Crypto;
using DiabetesBot.Utils.Logging; // ← твой логгер

namespace DiabetesBot.Services;

public class JsonStorageService
{
    private readonly string _dataDir;
    private readonly string _usersDir;

    private readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public JsonStorageService(string dataDir = "Data")
    {
        _dataDir = dataDir;
        _usersDir = Path.Combine(_dataDir, "users");

        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_usersDir);
    }

    // ================================================================
    // FOODS
    // ================================================================
    public List<FoodItem> LoadFoodItems()
    {
        string path1 = Path.Combine(_usersDir, "foods.json");
        string path2 = Path.Combine(_dataDir, "foods.json");

        string path = File.Exists(path1) ? path1 : path2;

        if (!File.Exists(path))
            throw new FileNotFoundException("foods.json NOT FOUND: " + path);

        return JsonSerializer.Deserialize<List<FoodItem>>(File.ReadAllText(path), _opts)
               ?? new List<FoodItem>();
    }

    public Dictionary<string, List<string>> LoadFoodCategories()
    {
        string path1 = Path.Combine(_usersDir, "food_categories.json");
        string path2 = Path.Combine(_dataDir, "food_categories.json");

        string path = File.Exists(path1) ? path1 : path2;

        if (!File.Exists(path))
            throw new FileNotFoundException("food_categories.json NOT FOUND: " + path);

        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path), _opts)
               ?? new Dictionary<string, List<string>>();
    }

    // ================================================================
    // USER STORAGE (encrypted)
    // ================================================================
    private string GetUserPath(long userId)
    {
        return Path.Combine(_usersDir, $"{userId}.json");
    }

    public UserData? LoadUser(long userId)
    {
        var path = GetUserPath(userId);

        if (!File.Exists(path))
            return null;

        string encrypted = File.ReadAllText(path);

        try
        {
            string json = EnvCrypto.Decrypt(encrypted);
            return JsonSerializer.Deserialize<UserData>(json);
        }
        catch (Exception ex)
        {
            BotLogger.Error($"[LOAD] Cannot decrypt user {userId}: {ex.Message}");
            return null;
        }
    }

    public void SaveUser(UserData user)
    {
        var path = GetUserPath(user.UserId);

        string json = JsonSerializer.Serialize(user, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        string encrypted = EnvCrypto.Encrypt(json);

        File.WriteAllText(path, encrypted);
    }
}
