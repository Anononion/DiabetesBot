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
        CommandHandler cmd,
        GlucoseModule glucose,
        BreadUnitsModule xe,
        DiabetesSchoolModule school)
    {
        _bot = bot;
        _cmd = cmd;
        _glucose = glucose;
        _xe = xe;
        _school = school;
    }

    public async Task HandleCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Data == null || cb.Message == null)
            return;

        long chatId = cb.Message.Chat.Id;
        long uid = cb.From.Id;
        string data = cb.Data;

        // текущее состояние пользователя
        var user = StateStore.Get(uid);

        BotLogger.Info($"[CB] {data}");

        // ============================
        // ШКОЛА ДИАБЕТА — старый стиль
        // ============================

        // Урок: "school_lesson:1"
        if (data.StartsWith("school_lesson:"))
        {
            string id = data.Split(':')[1];      // "1"
            await _cmd.HandleMessageAsync(Fake(chatId, uid, $"Урок {id}"), ct);
            return;
        }

        // Подурок: "school_sub:1.2"
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

        // ============================
        // ШКОЛА ДИАБЕТА — новый стиль DS_LESSON
        // ============================
        if (data.StartsWith("DS_LESSON"))
        {
            await _school.HandleCallbackAsync(user, cb, ct);
            return;
        }

        // ============================
        // ГЛЮКОЗА (типы измерений)
        // ============================
        if (data.StartsWith("GLU_TYPE"))
        {
            await _glucose.HandleCallbackAsync(user, cb, ct);
            return;
        }

        // ============================
        // ХЛЕБНЫЕ ЕДИНИЦЫ (XE)
        // ============================
        if (data.StartsWith("BU_CAT"))
        {
            await _xe.HandleCallbackAsync(user, cb, ct);
            return;
        }

        if (data.StartsWith("BU_ITEM"))
        {
            await _xe.HandleCallbackAsync(user, cb, ct);
            return;
        }

        if (data.StartsWith("XE_CAT") || data.StartsWith("XE_PROD"))
        {
        await _xe.HandleCallbackAsync(user, cb, ct);
        return;
        }


        // сюда позже можно добавить другие префиксы
        BotLogger.Warn($"[CB] Unknown callback: {data}");
    }

    private static Message Fake(long chatId, long uid, string text) =>
        new Message
        {
            Chat = new Chat { Id = chatId },
            From = new User { Id = uid },
            Text = text
        };
}

