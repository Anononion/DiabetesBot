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

    private Task<UserData> LoadAsync(long userId) =>
        _storage.LoadAsync(userId);

    private Task SaveAsync(UserData data) =>
        _storage.SaveAsync(data);

    public Task<UserData> GetStateAsync(long userId) =>
        LoadAsync(userId);

    public UserData GetState(long userId) =>
        GetStateAsync(userId).GetAwaiter().GetResult();

    // ========== LANGUAGE ==========

    public async Task<string> GetLanguageAsync(long userId)
    {
        var d = await LoadAsync(userId);
        return d.Language ?? "ru";
    }

    public async Task SetLanguageAsync(long userId, string lang)
    {
        var d = await LoadAsync(userId);
        d.Language = lang;
        await SaveAsync(d);
    }

    // ========== PHASE ==========

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

    // ========== STEP ==========

    public async Task SetStepAsync(long userId, UserStep step)
    {
        var d = await LoadAsync(userId);
        d.State.Step = step;
        await SaveAsync(d);
    }

    public void SetStep(long userId, UserStep step) =>
        SetStepAsync(userId, step).GetAwaiter().GetResult();

    // ========== TEMP ==========

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
}
