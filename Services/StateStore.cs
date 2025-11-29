using DiabetesBot.Models;

namespace DiabetesBot.Services;

public static class StateStore
{
    private static readonly Dictionary<long, UserData> _cache = new();
    private static JsonStorageService _storage = new();

    public static void Init(JsonStorageService storage)
    {
        _storage = storage;
    }

    public static UserData Get(long userId)
    {
        if (_cache.TryGetValue(userId, out var u))
            return u;

        // Load from file
        var loaded = _storage.LoadUser(userId);
        if (loaded != null)
        {
            _cache[userId] = loaded;
            return loaded;
        }

        // Create new
        var user = new UserData
        {
            UserId = userId,
            Language = "ru",
            Phase = BotPhase.MainMenu
        };

        _cache[userId] = user;
        _storage.SaveUser(user);

        return user;
    }

    public static void Save(UserData user)
    {
        _cache[user.UserId] = user;
        _storage.SaveUser(user);
    }

    public static UserData Reload(long userId)
    {
        var loaded = _storage.LoadUser(userId);

        if (loaded != null)
        {
            _cache[userId] = loaded;
            return loaded;
        }

        return Get(userId);
    }
}
