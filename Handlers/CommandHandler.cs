using Telegram.Bot;
using Telegram.Bot.Types;
using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Models;
using DiabetesBot.Utils;

namespace DiabetesBot.Handlers;

public class CommandHandler
{
    private readonly TelegramBotClient _bot;
    private readonly UserStateService _state;
    private readonly JsonStorageService _storage;

    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _bu;
    private readonly DiabetesSchoolModule _school;
    private readonly CallbackHandler _callback;

    public CommandHandler(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage,
        GlucoseModule glucose,
        BreadUnitsModule bu,
        DiabetesSchoolModule school,
        CallbackHandler callback)
    {
        _bot = bot;
        _state = state;
        _storage = storage;
        _glucose = glucose;
        _bu = bu;
        _school = school;
        _callback = callback;

        Logger.Info("[CMD] CommandHandler создан");
    }

    // Главное меню
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        var kb = KeyboardBuilder.MainMenu(lang);

        string txt = lang == "kk"
            ? "Бөлімді таңдаңыз:"
            : "Выберите раздел:";

        await _bot.SendMessage(chatId, txt, replyMarkup: kb, cancellationToken: ct);
    }

    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.Text is null) return;

        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;
        string text = msg.Text;

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language;

        // /start
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
            await _bot.SendMessage(chatId,
                "Выберите язык / Тілді таңдаңыз:",
                replyMarkup: KeyboardBuilder.LanguageChoice(),
                cancellationToken: ct);
            return;
        }

        // Пока выбирает язык
        if (await _state.GetPhaseAsync(userId) == UserPhase.ChoosingLanguage)
        {
            await _bot.SendMessage(chatId,
                lang == "kk"
                    ? "Тілді төмендегі батырмалар арқылы таңдаңыз."
                    : "Используйте кнопки ниже для выбора языка.",
                cancellationToken: ct);
            return;
        }

        // Главное меню
        if (text == KeyboardBuilder.Button_Glucose(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == KeyboardBuilder.Button_BreadUnits(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
            await _bu.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == KeyboardBuilder.Button_School(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.DiabetesSchool);
            await _school.ShowMainMenuAsync(chatId, userId, ct);
            return;
        }

        if (text == KeyboardBuilder.Button_Settings(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.Settings);
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Параметрлер:" : "Настройки:",
                replyMarkup: KeyboardBuilder.SettingsMenu(lang),
                cancellationToken: ct);
            return;
        }

        // Назад → главное меню
        if (text == KeyboardBuilder.Button_Back(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
            await SendMainMenuAsync(chatId, lang, ct);
            return;
        }

        // Глюкометрия ввод значения
        if (await _state.GetPhaseAsync(userId) == UserPhase.AwaitGlucoseValue)
        {
            await _glucose.HandleTextInputAsync(msg, ct);
            return;
        }

        // Хлебные единицы ввод веса
        if (_state.GetState(userId).State.Step == UserStep.BU_WaitWeight)
        {
            await _bu.HandleText(chatId, text, ct);
            return;
        }

        // Школа диабета ввод
        if (await _state.GetPhaseAsync(userId) == UserPhase.DiabetesSchool)
        {
            await _school.HandleMessageAsync(chatId, userId, text, ct);
            return;
        }

        // Неизвестная команда
        await _bot.SendMessage(chatId,
            lang == "kk"
                ? "Түсініксіз команда. Мәзірді пайдаланыңыз."
                : "Неизвестная команда. Используйте меню.",
            cancellationToken: ct);
    }
}
