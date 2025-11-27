using DiabetesBot.Models;
using DiabetesBot.Utils;
using DiabetesBot.Modules;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiabetesBot.Handlers;

public class CallbackHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _breadUnits;
    private readonly DiabetesSchoolModule _school;

    public CallbackHandler(
        ITelegramBotClient bot,
        GlucoseModule glucose,
        BreadUnitsModule breadUnits,
        DiabetesSchoolModule school)
    {
        _bot = bot;
        _glucose = glucose;
        _breadUnits = breadUnits;
        _school = school;
    }

    public async Task HandleCallbackAsync(CallbackQuery q, CancellationToken ct)
    {
        long userId = q.From.Id;
        long chatId = q.Message!.Chat.Id;
        string data = q.Data ?? "";

        var user = StateStore.Get(userId);

        BotLogger.Info($"[CB] DATA: '{data}' from {userId}");

        // ============================================================
        // ГЛЮКОЗА: выбор типа измерения
        // ============================================================
        if (data.StartsWith("GLU_TYPE|"))
        {
            string type = data.Split('|')[1];
            user.TempMeasurementType = type;
            user.Phase = BotPhase.Glucose_ValueInput;

            await _glucose.AskValueAsync(user, chatId, ct);

            await _bot.AnswerCallbackQuery(q.Id);
            return;
        }

        if (data == "GLU_SKIP")
        {
            user.Phase = BotPhase.Glucose;

            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Өткізілді." : "Пропущено.",
                cancellationToken: ct);

            await _bot.AnswerCallbackQuery(q.Id);
            return;
        }

        // ============================================================
        // ХЕ: выбор продукта
        // ============================================================
        if (data.StartsWith("XE_CAT|"))
        {
            string cat = data.Split('|')[1];

            await _breadUnits.ShowItemsByCategoryAsync(user, chatId, cat, ct);

            await _bot.AnswerCallbackQuery(q.Id);
            return;
        }

        if (data.StartsWith("XE_ITEM|"))
        {
            string itemId = data.Split('|')[1];

            await _breadUnits.SelectItemAsync(user, chatId, itemId, ct);

            await _bot.AnswerCallbackQuery(q.Id);
            return;
        }

        // ============================================================
        // ШКОЛА ДИАБЕТА: уроки, страницы
        // ============================================================
        if (data.StartsWith("SCH_LESSON|"))
        {
            string lessonId = data.Split('|')[1];

            await _school.OpenLessonAsync(user, chatId, lessonId, ct);

            await _bot.AnswerCallbackQuery(q.Id);
            return;
        }

        if (data.StartsWith("SCH_PAGE|"))
        {
            string[] parts = data.Split('|');
            string lessonId = parts[1];
            int page = int.Parse(parts[2]);

            await _school.ShowLessonPageAsync(user, chatId, lessonId, page, ct);

            await _bot.AnswerCallbackQuery(q.Id);
            return;
        }

        // ============================================================
        // НЕИЗВЕСТНЫЙ CALLBACK
        // ============================================================
        BotLogger.Warn($"[CB] UNKNOWN → '{data}'");
        await _bot.AnswerCallbackQuery(q.Id, "Unknown action");
    }
}

