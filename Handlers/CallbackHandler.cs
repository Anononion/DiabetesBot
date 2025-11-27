using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiabetesBot.Handlers;

public class CallbackHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly CommandHandler _commands;

    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _xe;
    private readonly DiabetesSchoolModule _school;

    public CallbackHandler(
        ITelegramBotClient bot,
        CommandHandler commands,
        GlucoseModule glucose,
        BreadUnitsModule xe,
        DiabetesSchoolModule school)
    {
        _bot = bot;
        _commands = commands;
        _glucose = glucose;
        _xe = xe;
        _school = school;
    }

    // ============================================================
    // MAIN ENTRY
    // ============================================================
    public async Task HandleAsync(CallbackQuery cb, CancellationToken ct)
    {
        long userId = cb.From.Id;
        long chatId = cb.Message!.Chat.Id;
        string data = cb.Data ?? "";

        BotLogger.Info($"[CB] DATA = {data}");

        var user = StateStore.Get(userId);

        // ------------------------------------------------------------
        // ХЕ – Категория
        // ------------------------------------------------------------
        if (data.StartsWith("xe_cat:"))
        {
            string category = data.Substring("xe_cat:".Length);
            await _xe.ShowItemsByCategoryAsync(user, chatId, category, ct);
            return;
        }

        // ------------------------------------------------------------
        // ХЕ – Продукт
        // ------------------------------------------------------------
        if (data.StartsWith("xe_item:"))
        {
            string itemName = data.Substring("xe_item:".Length);
            await _xe.SelectItemAsync(user, chatId, itemName, ct);
            return;
        }

        // ------------------------------------------------------------
        // ШКОЛА ДИАБЕТА – Переходы
        // ------------------------------------------------------------
        if (data.StartsWith("school_lesson:"))
        {
            // data: school_lesson:1
            string lessonIdStr = data.Substring("school_lesson:".Length);

            if (int.TryParse(lessonIdStr, out int lessonId))
            {
                user.CurrentLesson = lessonId;
                user.LessonPage = 0;
                await _school.ShowLessonPageAsync(user, chatId, ct);
            }
            return;
        }

        if (data == "school_next")
        {
            user.LessonPage++;
            await _school.ShowLessonPageAsync(user, chatId, ct);
            return;
        }

        if (data == "school_prev")
        {
            user.LessonPage--;
            await _school.ShowLessonPageAsync(user, chatId, ct);
            return;
        }

        // ------------------------------------------------------------
        // UNKNOWN
        // ------------------------------------------------------------
        BotLogger.Warn($"[CB] Unknown callback: {data}");
    }

    // ============================================================
    // UTILS
    // ============================================================
    private static Message FakeText(long chatId, long userId, string text)
    {
        return new Message
        {
            Chat = new Chat { Id = chatId },
            From = new User { Id = userId },
            Text = text
        };
    }
}
