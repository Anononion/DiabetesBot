using DiabetesBot.Services;
using System.Collections.Concurrent;
using DiabetesBot.Models;
using DiabetesBot.Utils


public static class StateStore
{
    private static readonly ConcurrentDictionary<long, UserData> _users = new();
    private static readonly JsonStorageService _storage = new JsonStorageService();

    // ------------------------------
    // Получение пользователя
    // ------------------------------
    public static UserData Get(long userId)
    {
        // Если уже есть в оперативке — отдаём сразу
        if (_users.TryGetValue(userId, out var cached))
            return cached;

        // Пытаемся загрузить из файла
        var loaded = _storage.LoadAsync(userId).Result;

        // Если файл не найден — создаём пустого
        if (loaded == null)
        {
            loaded = new UserData
            {
                UserId = userId,
                Language = "ru",
                Phase = BotPhase.MainMenu,
                Glucose = new(),
                BreadUnits = new()
            };

            // сразу сохраняем нового в файл
            _storage.SaveAsync(loaded).Wait();
        }

        _users[userId] = loaded;
        return loaded;
    }

    // ------------------------------
    // Сохранение пользователя
    // ------------------------------
    public static void Save(UserData user)
    {
        _users[user.UserId] = user;
        _storage.SaveAsync(user).Wait();
    }

    // ------------------------------
    // Принудительно перезагрузить из файла (если надо)
    // ------------------------------
    public static UserData Reload(long userId)
    {
        var loaded = _storage.LoadAsync(userId).Result;

        if (loaded != null)
        {
            _users[userId] = loaded;
            return loaded;
        }

        return Get(userId);
    }
    public static void SaveToFile(UserData user)
{
    try
    {
        JsonStorageService.SaveUserData(id, _users[id]);
    }
    catch (Exception ex)
    {
        BotLogger.Error($"[StateStore] SaveToFile error: {ex}");
    }
}

}
