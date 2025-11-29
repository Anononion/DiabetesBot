using DiabetesBot.Models;
using DiabetesBot.Services;

namespace DiabetesBot.Services;

public static class StateStore
{
    private static readonly Dictionary<long, UserData> _cache = new();
    private static JsonStorageService _storage = new();

    /// <summary>
    /// Инициализация (если понадобится DI — можно передать storage снаружи)
    /// </summary>
    public static void Init(JsonStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Получить пользователя из памяти или файла
    /// </summary>
    public static UserData Get(long userId)
    {
        // В кэше есть — отдаем
        if (_cache.TryGetValue(userId, out var u))
            return u;

        // Читаем из файла
        var loaded = _storage.LoadUserData(userId);

        if (loaded != null)
        {
            _cache[userId] = loaded;
            return loaded;
        }

        // Создаем нового
        var user = new UserData
        {
            UserId = userId,
            Language = "ru",
            Phase = BotPhase.MainMenu
        };

        _cache[userId] = user;
        _storage.SaveUserData(user);

        return user;
    }

    /// <summary>
    /// Сохранить изменения пользователя
    /// </summary>
    public static void Save(UserData user)
    {
        _cache[user.UserId] = user;
        _storage.SaveUserData(user);
    }

    /// <summary>
    /// Принудительная перезагрузка пользователя из файла
    /// </summary>
    public static UserData Reload(long userId)
    {
        var loaded = _storage.LoadUserData(userId);

        if (loaded != null)
        {
            _cache[userId] = loaded;
            return loaded;
        }

        return Get(userId);
    }
}
