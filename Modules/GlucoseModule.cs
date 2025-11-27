using DiabetesBot.Models;
using DiabetesBot.Modules;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiabetesBot.Handlers;

public class CallbackHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _bread;
    private readonly DiabetesSchoolModule _school;

    public CallbackHandler(
        ITelegramBotClient bot,
        GlucoseModule glucose,
        BreadUnitsModule bread,
        DiabetesSchoolModule school)
    {
        _bot = bot;
        _glucose = glucose;
        _bread = bread;
        _school = school;
    }

    // ==================================================================
    // MAIN ENTRY FOR CALLBACK QUERIES
    // ==================================================================
    public async Task HandleAsync(CallbackQuery q, CancellationToken ct)
    {
        long userId = q.From.Id;
        long chatId = q.Message!.Chat.Id;
        string data = q.Data ?? "";

        BotLogger.Info($"[CALLBACK] DATA='{data}', from={userId}");

        var user = StateStore.Get(userId);

        // Каждому модулю — свои пространства callback-данных
        if (data.StartsWith("GLU_"))
        {
            await HandleGlucoseAsync(user, q, chatId, ct);
            return;
        }

        if (data.StartsWith("XE_"))
        {
            await HandleBreadUnitsAsync(user, q, chatId, ct);
            return;
        }

        if (data.StartsWith("DS_"))
        {
            await HandleDiabetesSchoolAsync(user, q, chatId, ct);
            return;
        }

        BotLogger.Warn($"[CALLBACK] Unknown pattern: {data}");
    }

    // ==================================================================
    // GLUCOSE MODULE CALLBACKS
    // ==================================================================
    private async Task HandleGlucoseAsync(UserData user, CallbackQuery q, long chatId, CancellationToken ct)
    {
        string data = q.Data!;

        // примеры:
        // GLU_TYPE|fasting
        // GLU_TYPE|after
        // GLU_SKIP

        if (data.StartsWith("GLU_TYPE|"))
        {
            user.TempMeasurementType = data.Split('|')[1];
            user.Phase = BotPhase.Glucose_ValueInput;

            await _glucose.AskValueAsync(user, chatId, ct);
            await Answer(q);
            return;
        }

        if (data == "GLU_SKIP")
        {
            user.Phase = BotPhase.Glucose;

            await _bot.SendMessage(
                chatId,
                user.Language == "kz" ? "Өткізілді." : "Пропущено.",
                cancellationToken: ct);

            await _glucose.ShowMenuAsync(user, chatId, ct);
            await Answer(q);
            return;
        }

        BotLogger.Warn($"[CALLBACK] GLU Unknown: {data}");
    }

    // ==================================================================
    // BREAD UNITS CALLBACKS
    // ==================================================================
    private async Task HandleBreadUnitsAsync(UserData user, CallbackQuery q, long chatId, CancellationToken ct)
    {
        string data = q.Data!;

        // XE_CAT|<id>
        if (data.StartsWith("XE_CAT|"))
        {
            string category = data.Split('|')[1];

            user.LastSelectedCategory = category;
            user.Phase = BotPhase.BreadUnits;

            await _bread.ShowProductsAsync(user, chatId, category, ct);
            await Answer(q);
            return;
        }

        // XE_ITEM|<id>
        if (data.StartsWith("XE_ITEM|"))
        {
            string itemId = data.Split('|')[1];

            user.LastSelectedItemId = itemId;
            user.Phase = BotPhase.BreadUnits_EnterGrams;

            await _bread.AskGramsAsync(user, chatId, itemId, ct);
            await Answer(q);
            return;
        }

        BotLogger.Warn($"[CALLBACK] XE Unknown: {data}");
    }

    // ==================================================================
    // DIABETES SCHOOL CALLBACKS
    // ==================================================================
    private async Task HandleDiabetesSchoolAsync(UserData user, CallbackQuery q, long chatId, CancellationToken ct)
    {
        string data = q.Data!;

        // DS_LESSON|1
        if (data.StartsWith("DS_LESSON|"))
        {
            string lessonId = data.Split('|')[1];

            user.CurrentLessonId = lessonId;
            user.CurrentLessonPage = 0;
            user.Phase = BotPhase.DiabetesSchool;

            await _school.OpenLessonAsync(user, chatId, lessonId, ct);
            await Answer(q);
            return;
        }

        // DS_PAGE|N
        if (data.StartsWith("DS_PAGE|"))
        {
            if (int.TryParse(data.Split('|')[1], out int page))
            {
                user.CurrentLessonPage = page;
                user.Phase = BotPhase.DiabetesSchool;

                await _school.ShowLessonPageAsync(user, chatId, page, ct);
                await Answer(q);
                return;
            }
        }

        BotLogger.Warn($"[CALLBACK] DS Unknown: {data}");
    }

    // ==================================================================
    // QUICK CALLBACK RESPONSE (Telegram UI нужен)
    // ==================================================================
    private async Task Answer(CallbackQuery q)
    {
        try
        {
            await _bot.AnswerCallbackQuery(q.Id);
        }
        catch { }
    }
}
