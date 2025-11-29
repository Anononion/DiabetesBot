using System.Text.Json;
using DiabetesBot.Models;
using DiabetesBot.Utils.Crypto;

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

    // =====================================================================
    // LOAD FOODS / CATEGORIES
    // =====================================================================

    public List<FoodItem> LoadFoodItems()
    {
        string path1 = Path.Combine(_usersDir, "foods.json");
        string path2 = Path.Combine(_dataDir, "foods.json");

        string path = File.Exists(path1) ? path1 : path2;

        if (!File.Exists(path))
            throw new FileNotFoundException("foods.json NOT FOUND: " + path);

        string raw = File.ReadAllText(path);

        // продукты НЕ шифровались
        return JsonSerializer.Deserialize<List<FoodItem>>(raw, _opts)
               ?? new List<FoodItem>();
    }

    public Dictionary<string, List<string>> LoadFoodCategories()
    {
        string path1 = Path.Combine(_usersDir, "food_categories.json");
        string path2 = Path.Combine(_dataDir, "food_categories.json");

        string path = File.Exists(path1) ? path1 : path2;

        if (!File.Exists(path))
            throw new FileNotFoundException("food_categories.json NOT FOUND: " + path);

        string raw = File.ReadAllText(path);

        // категории тоже никогда не шифровались
        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(raw, _opts)
               ?? new Dictionary<string, List<string>>();
    }

    // =====================================================================
    // LOAD / SAVE USER (С ШИФРОВКОЙ!)
    // =====================================================================

    public UserData LoadUser(long userId)
    {
        string path = Path.Combine(_usersDir, $"{userId}.json");

        if (!File.Exists(path))
            return new UserData { UserId = userId };

        string encrypted = File.ReadAllText(path);

        // 🔥 пробуем расшифровать
        string? decrypted = EnvCrypto.TryDecrypt(encrypted);

        string json = decrypted ?? encrypted; // если файл был НЕ зашифрован

        return JsonSerializer.Deserialize<UserData>(json, _opts)
               ?? new UserData { UserId = userId };
    }

    public void SaveUser(UserData user)
    {
        string path = Path.Combine(_usersDir, $"{user.UserId}.json");

        string json = JsonSerializer.Serialize(user, _opts);

        // 🔥 ШИФРУЕМ КАК В СТАРОЙ ВЕРСИИ
        string encrypted = EnvCrypto.Encrypt(json);

        File.WriteAllText(path, encrypted);
    }
}

