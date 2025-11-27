using DiabetesBot.Models;
using DiabetesBot.Modules;
using DiabetesBot.Services;
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

    // ============================================================
    // MAIN ENTRY
    // ============================================================

    public async Task HandleCallbackAsync(CallbackQuery q, CancellationToken ct)
    {
        long userId = q.From.Id;
        var user = StateStore.Get(userId);

        string data = q.Data ?? "";
        BotLogger.Info($"[CALLBACK] From={userId} Data='{data}' Phase={user.Phase}");

        try
        {
            // ============================
            // ГЛЮКОЗА
            // ============================
            if (data.StartsWith("GLU_"))
            {
                await _glucose.HandleCallbackAsync(user, q, ct);
                return;
            }

            // ============================
            // ХЛЕБНЫЕ ЕДИНИЦЫ
            // ============================
            if (data.StartsWith("BU_"))
            {
                await _bread.HandleCallbackAsync(user, q, ct);
                return;
            }

            // ============================
            // ШКОЛА ДИАБЕТА
            // ============================
            if (data.StartsWith("DS_"))
            {
                await _school.HandleCallbackAsync(user, q, ct);
                return;
            }

            BotLogger.Warn($"[CALLBACK] Unknown callback prefix: '{data}'");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[CALLBACK] Error processing callback", ex);
        }
    }
}
