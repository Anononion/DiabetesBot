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

    public async Task HandleCallbackAsync(CallbackQuery q, CancellationToken ct)
    {
        long userId = q.From.Id;
        var user = StateStore.Get(userId);

        string data = q.Data ?? "";
        BotLogger.Info($"[CALLBACK] From={userId} Data='{data}' Phase={user.Phase}");

        try
        {
            switch (user.Phase)
            {
                // ============================
                // ГЛЮКОЗА
                // ============================
                case BotPhase.Glucose:
                case BotPhase.Glucose_TypeChoice:
                case BotPhase.Glucose_ValueInput:
                    if (data.StartsWith("GLU_"))
                    {
                        await _glucose.HandleCallbackAsync(user, q, ct);
                        return;
                    }
                    break;

                // ============================
                // ХЕ
                // ============================
                case BotPhase.BreadUnits:
                case BotPhase.BreadUnits_EnterGrams:
                    if (data.StartsWith("BU_"))
                    {
                        await _bread.HandleCallbackAsync(user, q, ct);
                        return;
                    }
                    break;

                // ============================
                // ШКОЛА ДИАБЕТА
                // ============================
                case BotPhase.DiabetesSchool:
                    if (data.StartsWith("DS_"))
                    {
                        await _school.HandleCallbackAsync(user, q, ct);
                        return;
                    }
                    break;

                default:
                    BotLogger.Warn($"[CALLBACK] Unexpected phase → ignoring. Phase={user.Phase}");
                    break;
            }

            BotLogger.Warn($"[CALLBACK] Unknown callback data '{data}'");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[CALLBACK] Error processing callback", ex);
        }
    }
}
