using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiabetesBot.Handlers;

public class CallbackHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly CommandHandler _cmd;
    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _xe;
    private readonly DiabetesSchoolModule _school;

    public CallbackHandler(
    ITelegramBotClient bot,
    GlucoseModule glucose,
    BreadUnitsModule xe,
    DiabetesSchoolModule school)
    {
        _bot = bot;
        _glucose = glucose;
        _xe = xe;
        _school = school;
    }

    public async Task HandleAsync(CallbackQuery cb, CancellationToken ct)
    {
        long chatId = cb.Message!.Chat.Id;
        long uid = cb.From.Id;
        string data = cb.Data ?? "";

        BotLogger.Info($"[CB] {data}");

        // Диабет-школа: Урок
        if (data.StartsWith("school_lesson:"))
        {
            string id = data.Split(':')[1];      // "1"
            await _cmd.HandleMessageAsync(Fake(chatId, uid, $"Урок {id}"), ct);
            return;
        }

        // Диабет-школа: Подурок
        if (data.StartsWith("school_sub:"))
        {
            string raw = data.Split(':')[1];    // "1.2"
            await _cmd.HandleMessageAsync(Fake(chatId, uid, raw), ct);
            return;
        }

        if (data == "school_next")
        {
            await _cmd.HandleMessageAsync(Fake(chatId, uid, "Далее"), ct);
            return;
        }

        if (data == "school_prev")
        {
            await _cmd.HandleMessageAsync(Fake(chatId, uid, "Назад"), ct);
            return;
        }
    }

    public Task HandleCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        return HandleAsync(cb, ct);
    }


    private static Message Fake(long chatId, long uid, string text)
        => new Message
        {
            Chat = new Chat { Id = chatId },
            From = new User { Id = uid },
            Text = text
        };
}


