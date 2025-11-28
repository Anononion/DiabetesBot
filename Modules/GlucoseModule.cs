using System.Globalization;
using System.Linq;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

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
    // Обработка текстовых команд (фаза Glucose)
    // ---------------------------------------------------------
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        // Локальный "назад" на всякий случай
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
            user.Phase = BotPhase.Glucose_TypeSelect;
            await AskTypeAsync(user, chatId, ct);
            return;
        }

        // Если непонятно что — просто перерисуем меню
        await ShowMenuAsync(user, chatId, ct);
    }

    
    public async Task HandleCallbackAsync(UserData user, CallbackQuery cb, CancellationToken ct)
    {
        string data = cb.Data ?? "";
        long chatId = cb.Message.Chat.Id;
    
        // если это не наши кнопки — выходим
        if (!data.StartsWith("GLU_TYPE:"))
            return;

        string type = data.Split(':')[1];

        if (type == "cancel")
        {
            user.TempGlucoseType = null;
            user.Phase = BotPhase.Glucose;

            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        // сохраняем тип
        user.TempGlucoseType = type;
        user.Phase = BotPhase.Glucose_ValueInput;

        // спрашиваем значение
        await _bot.SendMessage(chatId,
            user.Language == "ru"
                ? "Введите значение глюкозы:"
                : "Глюкоза мәнін енгізіңіз:",
            cancellationToken: ct);
    }



    // ---------------------------------------------------------
    // Ввод значения (фаза Glucose_ValueInput)
    // ---------------------------------------------------------
    public async Task AskValueAsync(UserData user, long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Мәнді енгізіңіз:" : "Введите значение:",
            cancellationToken: ct);
    }

    private async Task AskTypeAsync(UserData user, long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId,
            user.Language == "ru" ? "Выберите тип измерения:" : "Өлшеу түрін таңдаңыз:",
            replyMarkup: BuildTypeKeyboard(user),
            cancellationToken: ct);
    }

    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        // Нормализуем точку/запятую
        var normalized = text.Replace(',', '.');

        if (!double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct);
            return;
        }

        // Записываем в текущую модель: GlucoseRecord (Value + Time)
        user.Glucose.Add(new GlucoseRecord
        {
            Value = value,
            Type  = user.TempGlucoseType ?? "",
            Time  = DateTime.UtcNow
        });

        user.TempGlucoseType = null;

        user.Phase = BotPhase.Glucose;

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Сақталды!" : "Сохранено!",
            cancellationToken: ct);

        await ShowMenuAsync(user, chatId, ct);
    }

    private InlineKeyboardMarkup BuildTypeKeyboard(UserData user)
    {
        bool ru = user.Language == "ru";

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(ru ? "🕒 Натощак"   : "🕒 Ашқарын",   "GLU_TYPE:fasting"),
                InlineKeyboardButton.WithCallbackData(ru ? "🍽 После еды" : "🍽 Тамақтан соң", "GLU_TYPE:after")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(ru ? "⏱ По времени" : "⏱ Уақыт бойынша", "GLU_TYPE:time")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(ru ? "❌ Отмена" : "❌ Болдырмау", "GLU_TYPE:cancel")
            }
        });
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
            user.Glucose
                .OrderByDescending(x => x.Time)
                .Take(10)
                .Select(x =>
                {
                    var t = x.Time.ToLocalTime();
                    // Если когда-нибудь начнём заполнять Type — выводим
                    var typePart = string.IsNullOrWhiteSpace(x.Type) ? "" : $" ({x.Type})";
                    return $"{t:dd.MM HH:mm} — {x.Value:0.0}{typePart}";
                })
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




