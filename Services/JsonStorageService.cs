using System.Text.Json;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using DiabetesBot.Utils.Crypto;

namespace DiabetesBot.Services;

public class JsonStorageService
{
    // ---- определяем корень проекта ----
    private static string ProjectRoot
    {
        get
        {
            string dir = AppContext.BaseDirectory;

            while (dir != null && !Directory.GetFiles(dir).Any(f => f.EndsWith("Program.cs")))
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

    private static void DebugLog(string msg)
    {
        try
        {
            Directory.CreateDirectory(LogsDir);
            File.AppendAllText(
                Path.Combine(LogsDir, "debug_paths.log"),
                $"{DateTime.Now:O} {msg}{Environment.NewLine}"
            );
        }
        catch { }
    }

    // ---------------- user main file ----------------

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

    // ---------------- user additional files ----------------

    private string UserFolder(long id) => Path.Combine(UsersDir, id.ToString());
    private string FilePath(long id, string name) => Path.Combine(UserFolder(id), name);

    public List<T> LoadUserFile<T>(long userId, string fileName)
    {
        Directory.CreateDirectory(UserFolder(userId));
        string path = FilePath(userId, fileName);

        if (!File.Exists(path))
            return new List<T>();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
    }

    public void SaveUserFile<T>(long userId, string fileName, List<T> list)
    {
        Directory.CreateDirectory(UserFolder(userId));
        string path = FilePath(userId, fileName);

        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    // ---------------- XE history ----------------

    public List<XeRecord> LoadXeHistory(long userId)
        => LoadUserFile<XeRecord>(userId, "xe_history.json");

    public void AppendXeRecord(long userId, XeRecord record)
    {
        var list = LoadXeHistory(userId);
        list.Add(record);
        SaveUserFile(userId, "xe_history.json", list);
    }

    // ---------------- STATIC JSONS (foods.json, categories) ----------------

    public List<FoodItem> LoadFoodItems()
    {
        string path = Path.Combine(UsersDir, "foods.json");

        DebugLog($"LoadFoodItems: ProjectRoot={ProjectRoot}, UsersDir={UsersDir}, Path={path}, Exists={File.Exists(path)}");

        if (!File.Exists(path))
            throw new FileNotFoundException($"foods.json НЕ найден: {path}");

        var raw = File.ReadAllText(path);
        Logger.Info($"[BU] foods.json RAW:\n{raw}");
        return JsonSerializer.Deserialize<List<FoodItem>>(raw)!;
    }

    public Dictionary<string, List<string>> LoadFoodCategories()
    {
        string path = Path.Combine(UsersDir, "food_categories.json");

        DebugLog($"LoadFoodCategories: ProjectRoot={ProjectRoot}, UsersDir={UsersDir}, Path={path}, Exists={File.Exists(path)}");

        if (!File.Exists(path))
            throw new FileNotFoundException($"food_categories.json НЕ найден: {path}");

        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path))!;
    }
}
