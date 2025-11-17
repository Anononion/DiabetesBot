using Telegram.Bot;
using Telegram.Bot.Types;
using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Models;
using DiabetesBot.Utils;

namespace DiabetesBot.Handlers;

public class CallbackHandler
{
    private readonly TelegramBotClient _bot;
    private readonly UserStateService _state;
    private readonly JsonStorageService _storage;

    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _bu;
    private readonly DiabetesSchoolModule _school;

    private CommandHandler? _commandHandler;

    public CallbackHandler(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage,
        GlucoseModule glucose,
        BreadUnitsModule bu,
        DiabetesSchoolModule school)
    {
        _bot = bot;
        _state = state;
        _storage = storage;
        _glucose = glucose;
        _bu = bu;
        _school = school;

        Logger.Info("[CB] CallbackHandler создан");
    }

    public void SetCommandHandler(CommandHandler handler)
    {
        _commandHandler = handler;
        Logger.Info("[CB] CommandHandler привязан к CallbackHandler");
    }

    public async Task HandleAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Data is null)
        {
            Logger.Warn("[CB] HandleAsync: query.Data is null, игнорируем");
            return;
        }

        string data = query.Data;
        long chatId = query.Message!.Chat.Id;
        long userId = query.From.Id;

        Logger.Info($"[CB] HandleAsync: userId={userId}, chatId={chatId}, data='{data}'");

        // === выбор языка ===
        if (data == "lang_ru" || data == "lang_kk")
        {
            Logger.Info($"[CB] Обработка выбора языка: data='{data}' для userId={userId}");

            var user = await _storage.LoadAsync(userId);
            user.Language = data == "lang_ru" ? "ru" : "kk";
            await _storage.SaveAsync(user);

            await _state.SetPhaseAsync(userId, UserPhase.MainMenu);

            string msg = user.Language == "ru"
                ? "Язык успешно изменён 🇷🇺"
                : "Тіл сәтті өзгертілді 🇰🇿";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);

            if (_commandHandler != null)
            {
                Logger.Info($"[CB] Выбор языка завершён, вызываем SendMainMenuAsync для userId={userId}");
                await _commandHandler.SendMainMenuAsync(chatId, user.Language, ct);
            }
            else
            {
                Logger.Warn("[CB] _commandHandler == null при выборе языка");
            }

            Logger.Info($"[CB] User {userId} changed language to {user.Language}");
            return;
        }

        // === глюкометрия: выбор типа измерения ===
        if (data.StartsWith("measure_"))
        {
            Logger.Info($"[CB] Глюкометрия callback: data='{data}', передаём в GlucoseModule.HandleCallbackAsync");
            await _glucose.HandleCallbackAsync(query, ct);
            return;
        }

        // === хлебные единицы: категории/продукты ===
        if (data.StartsWith("BU_"))
        {
            Logger.Info($"[CB] ХЕ callback: data='{data}', передаём в BreadUnitsModule.HandleButton");
            await _bu.HandleButton(chatId, data, ct);
            return;
        }

        // === школа диабета ===
        if (data.StartsWith("DS_"))
        {
            Logger.Info($"[CB] Школа диабета callback: data='{data}', передаём в DiabetesSchoolModule.HandleCallbackAsync");
            await _school.HandleCallbackAsync(query, ct);
            return;
        }

        // === ШКОЛА ДИАБЕТА: выбор главы ===
        if (data.StartsWith("DS_CHAPTER|"))
        {
            var payload = data.Replace("DS_CHAPTER|", "");
            if (int.TryParse(payload, out var chapter))
            {
                Logger.Info($"[CB] DS_CHAPTER selected: {chapter}");
                await _school.ShowChapterMenuAsync(chatId, chapter, ct);
            }
            else
            {
                Logger.Warn($"[CB] DS_CHAPTER parse error: {data}");
            }
            return;
        }

        // === ШКОЛА ДИАБЕТА: выбор урока ===
        if (data.StartsWith("DS_LESSON|"))
        {
            var lessonId = data.Replace("DS_LESSON|", "");
            Logger.Info($"[CB] DS_LESSON selected: {lessonId}");
            await _school.ShowLessonTextAsync(chatId, userId, lessonId, ct);
            return;
        }

        // === ШКОЛА ДИАБЕТА: назад к главам ===
        if (data == "DS_BACK_TO_CHAPTERS")
        {
            Logger.Info("[CB] Back to DS chapters");
            await _school.ShowMainMenuAsync(chatId, userId, ct);
            return;
        }


        Logger.Warn($"[CB] Неизвестный callback data='{data}'");
    }
}
