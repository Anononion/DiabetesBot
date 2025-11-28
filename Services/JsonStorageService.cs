using System.Text.Json;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using DiabetesBot.Utils.Crypto;

namespace DiabetesBot.Services;

public class JsonStorageService
{
    private static string ProjectRoot
    {
        get
        {
            string dir = AppContext.BaseDirectory;

            while (dir != null && !Directory.GetFiles(dir)
                       .Any(f => f.EndsWith("Program.cs")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }

            return dir ?? AppContext.BaseDirectory;
        }
    }

    private static string DataDir => Path.Combine(ProjectRoot, "Data");
    private static string UsersDir => Path.Combine(DataDir, "users");
    private static string LogsDir => Path.Combine(DataDir, "logs");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // -----------------------------------------------------------
    // USER MAIN JSON (encrypted)
    // -----------------------------------------------------------

    public async Task<UserData> LoadAsync(long userId)
    {
        Directory.CreateDirectory(UsersDir);
        string path = Path.Combine(UsersDir, $"{userId}.json");

        if (!File.Exists(path))
            return new UserData { UserId = userId };

        string raw = await File.ReadAllTextAsync(path);

        string json = raw;

        bool isBase64 =
            raw.Length % 4 == 0 &&
            raw.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=');

        try
        {
            if (isBase64)
                json = EnvCrypto.Decrypt(raw);
        }
        catch
        {
            json = raw;
        }

        return JsonSerializer.Deserialize<UserData>(json, JsonOpts)
               ?? new UserData { UserId = userId };
    }

    public async Task SaveAsync(UserData data)
    {
        Directory.CreateDirectory(UsersDir);
        string path = Path.Combine(UsersDir, $"{data.UserId}.json");

        string json = JsonSerializer.Serialize(data, JsonOpts);
        string encrypted = EnvCrypto.Encrypt(json);

        await File.WriteAllTextAsync(path, encrypted);
    }

    // -----------------------------------------------------------
    // USER SUBFILES (plain JSON)
    // -----------------------------------------------------------

    private string UserFolder(long id) => Path.Combine(UsersDir, id.ToString());
    private string FilePath(long id, string name) => Path.Combine(UserFolder(id), name);

    public List<T> LoadUserFile<T>(long userId, string fileName)
    {
        Directory.CreateDirectory(UserFolder(userId));
        string path = FilePath(userId, fileName);

        if (!File.Exists(path))
            return new List<T>();

        return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path))
               ?? new List<T>();
    }

    public void SaveUserFile<T>(long userId, string fileName, List<T> list)
    {
        Directory.CreateDirectory(UserFolder(userId));
        string path = FilePath(userId, fileName);

        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }

    // -----------------------------------------------------------
    // XE HISTORY
    // -----------------------------------------------------------

    public List<XeRecord> LoadXeHistory(long userId)
        => LoadUserFile<XeRecord>(userId, "xe_history.json");

    public void AppendXeRecord(long userId, XeRecord record)
    {
        var list = LoadXeHistory(userId);
        list.Add(record);
        SaveUserFile(userId, "xe_history.json", list);
    }

    // -----------------------------------------------------------
    // STATIC JSON (foods / categories)
    // -----------------------------------------------------------

    public List<FoodItem> LoadFoodItems()
    {
        string path = Path.Combine(UsersDir, "foods.json");

        if (!File.Exists(path))
            throw new FileNotFoundException("foods.json NOT FOUND: " + path);

        return JsonSerializer.Deserialize<List<FoodItem>>(File.ReadAllText(path))!;
    }

    public Dictionary<string, List<string>> LoadFoodCategories()
    {
        string path = Path.Combine(UsersDir, "food_categories.json");

        if (!File.Exists(path))
            throw new FileNotFoundException("food_categories.json NOT FOUND: " + path);

        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path))!;
    }
}
