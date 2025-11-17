using DiabetesBot.Models;
using System.Text.Json;

namespace DiabetesBot.Services;

public class UserStateService
{
    private readonly JsonStorageService _storage;

    public UserStateService(JsonStorageService storage)
    {
        _storage = storage;
    }

    // ========= базовые загрузка / сохранение =========

    private Task<UserData> LoadAsync(long userId) =>
        _storage.LoadAsync(userId);

    private Task SaveAsync(UserData data) =>
        _storage.SaveAsync(data);

    // ========= чтение состояния целиком =========

    // async-вариант
    public Task<UserData> GetStateAsync(long userId) =>
        LoadAsync(userId);

    // синхронная обёртка (чтобы старый код с GetState не ломать)
    public UserData GetState(long userId) =>
        GetStateAsync(userId).GetAwaiter().GetResult();

    // ========= Phase =========

    public async Task<UserPhase> GetPhaseAsync(long userId)
    {
        var d = await LoadAsync(userId);
        return d.Phase;
    }

    public async Task SetPhaseAsync(long userId, UserPhase phase)
    {
        var d = await LoadAsync(userId);
        d.Phase = phase;
        await SaveAsync(d);
    }

    // ========= Step =========

    public async Task SetStepAsync(long userId, UserStep step)
    {
        var d = await LoadAsync(userId);
        d.State.Step = step;
        await SaveAsync(d);
    }

    // синхронная обёртка для существующего кода
    public void SetStep(long userId, UserStep step) =>
        SetStepAsync(userId, step).GetAwaiter().GetResult();

    // ========= Clear =========

    public async Task ClearAsync(long userId)
    {
        var d = await LoadAsync(userId);
        d.State = new UserState();
        await SaveAsync(d);
    }

    public void Clear(long userId) =>
        ClearAsync(userId).GetAwaiter().GetResult();

    // ========= Temp =========

    public async Task TempSetAsync(long userId, string key, string value)
    {
        var d = await LoadAsync(userId);
        d.State.Temp[key] = value;
        await SaveAsync(d);
    }

    public async Task<string> TempGetAsync(long userId, string key)
    {
        var d = await LoadAsync(userId);
        return d.State.Temp.TryGetValue(key, out var v) ? v : "";
    }

    public void TempString(long userId, string key, string value) =>
        TempSetAsync(userId, key, value).GetAwaiter().GetResult();

    public string TempString(long userId, string key) =>
        TempGetAsync(userId, key).GetAwaiter().GetResult();

    // ========= Temp list =========

    public async Task SetTempListAsync<T>(long userId, List<T> list)
    {
        var json = JsonSerializer.Serialize(list);
        await TempSetAsync(userId, "list", json);
    }

    public async Task<List<T>> GetTempListAsync<T>(long userId)
    {
        var d = await LoadAsync(userId);

        if (!d.State.Temp.TryGetValue("list", out var raw))
            return new List<T>();

        return JsonSerializer.Deserialize<List<T>>(raw) ?? new List<T>();
    }

    public void SetTempList<T>(long userId, List<T> list) =>
        SetTempListAsync(userId, list).GetAwaiter().GetResult();

    public List<T> GetTempList<T>(long userId) =>
        GetTempListAsync<T>(userId).GetAwaiter().GetResult();
}
