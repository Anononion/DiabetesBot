using DiabetesBot.Services;
using DiabetesBot.Utils;
using DiabetesBot.Modules;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiabetesBot.Handlers;

public class CallbackHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly CommandHandler _commands;

    public CallbackHandler(ITelegramBotClient bot, CommandHandler commands)
    {
        _bot = bot;
        _commands = commands;
    }

    public async Task HandleAsync(CallbackQuery cb, CancellationToken ct)
    {
        long userId = cb.From.Id;
        long chatId = cb.Message!.Chat.Id;
        string data = cb.Data ?? "";

        BotLogger.Info($"[CB] DATA = {data}");

        // =========================
        // ХЕ — Категории старого бота
        // =========================
        if (data.StartsWith("xe_cat:"))
        {
            string categoryName = data.Substring("xe_cat:".Length);
            await RedirectTextAsync(chatId, userId, categoryName, ct);
            return;
        }

        // =========================
        // ХЕ — продукт (старый бот → просто текст)
        // =========================
        if (data.StartsWith("xe_item:"))
        {
            string foodName = data.Substring("xe_item:".Length);
            await RedirectTextAsync(chatId, userId, foodName, ct);
            return;
        }

        // =========================
        // Школа диабета — Урок N
        // =========================
        if (data.StartsWith("school_lesson:"))
        {
            string lessonTitle = data.Substring("school_lesson:".Length);
            await RedirectTextAsync(chatId, userId, lessonTitle, ct);
            return;
        }

        // =========================
        // Школа диабета — навигация (старый бот принимает "Далее"/"Назад")
        // =========================
        if (data == "school_next")
        {
            await RedirectTextAsync(chatId, userId, "Далее", ct);
            return;
        }

        if (data == "school_prev")
        {
            await RedirectTextAsync(chatId, userId, "Назад", ct);
            return;
        }

        BotLogger.Warn($"[CB] Unknown callback: {data}");
    }

    private async Task RedirectTextAsync(long chatId, long userId, string text, CancellationToken ct)
    {
        await _commands.HandleMessageAsync(
            new Message
            {
                Chat = new Chat { Id = chatId },
                From = new User { Id = userId },
                Text = text
            },
            ct
        );
    }
}
