using System.Text.Json;
using DiabetesBot.Models;
using DiabetesBot.Utils.Crypto;

public static class StateStore
{
    private static readonly Dictionary<long, UserData> _users = new();
    private static readonly string Dir = "Data/users";

    static StateStore()
    {
        if (!Directory.Exists(Dir))
            Directory.CreateDirectory(Dir);
    }

    // -----------------------------
    // Получить пользователя
    // -----------------------------
    public static UserData Get(long id)
    {
        if (_users.TryGetValue(id, out var user))
            return user;

        user = LoadFromFile(id);
        _users[id] = user;

        return user;
    }

    // -----------------------------
    // Сохранение
    // -----------------------------
    public static void Save(long id, UserData user)
    {
        try
        {
            var json = JsonSerializer.Serialize(user, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var encrypted = EnvCrypto.Encrypt(json);

            File.WriteAllText(Path.Combine(Dir, $"{id}.json"), encrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ERR] Save failed: " + ex.Message);
        }
    }

    // -----------------------------
    // Загрузка
    // -----------------------------
    private static UserData LoadFromFile(long id)
    {
        try
        {
            var path = Path.Combine(Dir, $"{id}.json");
            if (!File.Exists(path))
            {
                return new UserData
                {
                    UserId = id,
                    Language = "ru",
                    Phase = BotPhase.MainMenu
                };
            }

            var encrypted = File.ReadAllText(path);
            var json = EnvCrypto.Decrypt(encrypted);

            var user = JsonSerializer.Deserialize<UserData>(json);
            if (user != null)
                return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ERR] Load failed: " + ex.Message);
        }

        // Fallback если файл битый
        return new UserData
        {
            UserId = id,
            Language = "ru",
            Phase = BotPhase.MainMenu
        };
    }
}
