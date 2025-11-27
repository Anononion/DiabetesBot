using DiabetesBot.Models;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Modules;

public class GlucoseModule
{
    private readonly ITelegramBotClient _bot;

    public GlucoseModule(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    // ---------------------------------------------------------
    // Главное меню Глюкозы
    // ---------------------------------------------------------
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[GLU] ShowMenu");

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📋 История", "📊 Статистика" },
            new KeyboardButton[] { "➕ Добавить измерение" },
            new KeyboardButton[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" }
        })
        {
            ResizeKeyboard = true
        };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Глюкоза мәзірі:" : "Меню глюкозы:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // Обработка текстовых команд
    // ---------------------------------------------------------
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        if (text.Contains("История"))
        {
            await SendHistoryAsync(user, chatId, ct);
            return;
        }

        if (text.Contains("Статистика"))
        {
            await SendStatsAsync(user, chatId, ct);
            return;
        }

        if (text.Contains("Добавить"))
        {
            user.Phase = BotPhase.Glucose_ValueInput;
            await AskValueAsync(user, chatId, ct);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    // Ввод значения
    // ---------------------------------------------------------
    public async Task AskValueAsync(UserData user, long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Мәнді енгізіңіз:" : "Введите значение:",
            cancellationToken: ct);
    }

    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (!double.TryParse(text.Replace(",", "."), out double value))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct);
            return;
        }

        user.Glucose.Add(new GlucoseEntry
{
    Value = (int)Math.Round(value),
    Timestamp = DateTime.UtcNow
});


        user.Phase = BotPhase.Glucose;

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Сақталды!" : "Сохранено!",
            cancellationToken: ct);

        await ShowMenuAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    // История
    // ---------------------------------------------------------
    private async Task SendHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Glucose.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Өлшеулер жоқ." : "Нет измерений.",
                cancellationToken: ct);
            return;
        }

        string msg = string.Join(
            "\n",
            user.Glucose.OrderByDescending(x => x.Timestamp).Take(10)
                .Select(x => $"{x.Value} — {x.Timestamp:dd.MM HH:mm}")
        );

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // Статистика
    // ---------------------------------------------------------
    private async Task SendStatsAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Glucose.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Статистика жоқ." : "Статистики нет.",
                cancellationToken: ct);
            return;
        }

        var arr = user.Glucose.Select(x => x.Value).ToArray();
        double avg = arr.Average();

        await _bot.SendMessage(chatId,
            (user.Language == "kz" ? "Орташа мән: " : "Среднее значение: ") + avg.ToString("0.0"),
            cancellationToken: ct);
    }
}

