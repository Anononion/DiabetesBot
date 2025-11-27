using DiabetesBot.Models;
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
        // ГЛЮКОЗА
        // ------------------------------------------------------------
        if (data == "glu_fasting")
        {
            await _commands.HandleMessageAsync(
                FakeTextMessage(chatId, userId, "Натощак"), ct);
            return;
        }

        if (data == "glu_after")
        {
            await _commands.HandleMessageAsync(
                FakeTextMessage(chatId, userId, "После еды"), ct);
            return;
        }

        if (data == "glu_time")
        {
            await _commands.HandleMessageAsync(
                FakeTextMessage(chatId, userId, "По времени"), ct);
            return;
        }

        if (data == "glu_skip")
        {
            await _commands.HandleMessageAsync(
                FakeTextMessage(chatId, userId, "Пропустить"), ct);
            return;
        }

        // ------------------------------------------------------------
        // ХЕ – Категории
        // ------------------------------------------------------------
        if (data.StartsWith("xe_cat:"))
        {
            string id = data.Split(':')[1];

            string? title = FoodCache.GetCategoryTitle(id, user.Language);

            if (title != null)
            {
                await _commands.HandleMessageAsync(
                    FakeTextMessage(chatId, userId, title), ct);
            }
            return;
        }

        // ------------------------------------------------------------
        // ХЕ – Конкретный продукт
        // ------------------------------------------------------------
        if (data.StartsWith("xe_food:"))
        {
            string id = data.Split(':')[1];

            string? title = FoodCache.GetFoodTitle(id);

            if (title != null)
            {
                await _commands.HandleMessageAsync(
                    FakeTextMessage(chatId, userId, title), ct);
            }
            return;
        }

        // ------------------------------------------------------------
        // ШКОЛА ДИАБЕТА – УРОК
        // ------------------------------------------------------------
        if (data.StartsWith("school_lesson:"))
        {
            int lessonId = int.Parse(data.Split(':')[1]);

            string title = _school.GetLessonButtonText(lessonId, user.Language);

            await _commands.HandleMessageAsync(
                FakeTextMessage(chatId, userId, title), ct);

            return;
        }

        if (data == "school_next")
        {
            await _commands.HandleMessageAsync(
                FakeTextMessage(chatId, userId, "Далее"), ct);
            return;
        }

        if (data == "school_prev")
        {
            await _commands.HandleMessageAsync(
                FakeTextMessage(chatId, userId, "Назад"), ct);
            return;
        }

        // ------------------------------------------------------------
        // Если что-то неизвестно — игнор
        // ------------------------------------------------------------
        BotLogger.Warn($"[CB] Unknown callback: {data}");
    }

    // ================================================================
    // UTILITY – создаём Message, как будто пользователь отправил текст
    // ================================================================
    private static Message FakeTextMessage(long chatId, long userId, string text)
    {
        return new Message
        {
            Chat = new Chat { Id = chatId },
            From = new User { Id = userId },
            Text = text
        };
    }
}
