using System.IO;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
using DiabetesBot.Services;
using DiabetesBot.Utils;

namespace DiabetesBot.Modules;

public class BreadUnitsModule
{
    private readonly ITelegramBotClient _bot;

    private Dictionary<string, List<string>> _categories = new();
    private List<FoodItem> _foods = new();

    public BreadUnitsModule(ITelegramBotClient bot)
    {
        _bot = bot;

        BotLogger.Info("[BU] Initializing BreadUnitsModule…");
        LoadFoodData();
    }

    // ============================================================
    // LOAD JSON
    // ============================================================
    private void LoadFoodData()
    {
        try
        {
            string catPath = Path.Combine(AppContext.BaseDirectory, "Data", "food_categories.json");
            string foodsPath = Path.Combine(AppContext.BaseDirectory, "Data", "foods.json");

            _categories = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
                File.ReadAllText(catPath)
            ) ?? new();

            _foods = JsonSerializer.Deserialize<List<FoodItem>>(
                File.ReadAllText(foodsPath)
            ) ?? new();

            BotLogger.Info($"[BU] categories loaded: {_categories.Count}");
            BotLogger.Info($"[BU] foods loaded: {_foods.Count}");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[BU] Failed to load food data", ex);
        }
    }

    // ============================================================
    // MAIN MENU OF THE MODULE
    // ============================================================
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[BU] ShowMenu");

        string add = user.Language == "kz" ? "➕ Өнім қосу" : "➕ Добавить продукт";
        string history = user.Language == "kz" ? "📄 ХЕ тарихы" : "📄 История ХЕ";
        string back = user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(add) },
            new[] { new KeyboardButton(history) },
            new[] { new KeyboardButton(back) }
        })
        {
            ResizeKeyboard = true
        };

        string msg = user.Language == "kz"
            ? "🥖 ХЕ — әрекетті таңдаңыз:"
            : "🥖 ХЕ — выберите действие:";

        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);
    }

    // ============================================================
    // HANDLE TEXT
    // ============================================================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[BU] HandleText '{text}'");

        string add = user.Language == "kz" ? "➕ Өнім қосу" : "➕ Добавить продукт";
        string history = user.Language == "kz" ? "📄 ХЕ тарихы" : "📄 История ХЕ";
        string back = user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        if (text == add)
        {
            await ShowCategoryMenuAsync(user, chatId, ct);
            return;
        }

        if (text == history)
        {
            await ShowHistoryAsync(user, chatId, ct);
            return;
        }

        if (text == back)
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // ============================================================
    // CATEGORIES
    // ============================================================
    private async Task ShowCategoryMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[BU] ShowCategoryMenu");

        var rows = _categories.Keys
            .Select(cat => new[]
            {
                InlineKeyboardButton.WithCallbackData(cat, $"BU_CAT|{cat}")
            })
            .ToList();

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(
                user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад",
                "BU_BACK_MAIN"
            )
        });

        string msg = user.Language == "kz"
            ? "Санатты таңдаңыз:"
            : "Выберите категорию:";

        await _bot.SendMessage(chatId, msg,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // ============================================================
    // PRODUCTS INSIDE CATEGORY
    // ============================================================
    private async Task ShowProductsAsync(UserData user, long chatId, string cat, CancellationToken ct)
    {
        BotLogger.Info($"[BU] ShowProducts in category '{cat}'");

        if (!_categories.ContainsKey(cat))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Санат табылмады." : "Категория не найдена.",
                cancellationToken: ct);
            return;
        }

        var productIds = _categories[cat];

        var products = _foods
            .Where(f => productIds.Contains(f.Id))
            .ToList();

        var rows = products
            .Select(f =>
                new[] { InlineKeyboardButton.WithCallbackData(
                    user.Language == "kz" ? f.name_kk : f.name_ru,
                    $"BU_PROD|{f.Id}") })
            .ToList();

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(
                user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад",
                "BU_BACK_CAT")
        });

        await _bot.SendMessage(chatId, cat,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // ============================================================
    // ASK GRAMS
    // ============================================================
    private async Task AskGramsAsync(UserData user, long chatId, FoodItem item, CancellationToken ct)
    {
        BotLogger.Info($"[BU] Ask grams for product {item.Id}");

        user.TempSelectedFoodId = item.Id;
        user.Phase = BotPhase.BreadUnits_EnterGrams;

        string msg = user.Language == "kz"
            ? $"*{item.name_kk}* — грамм енгізіңіз:"
            : $"Введите граммы продукта *{item.name_ru}*:";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // ============================================================
    // HANDLE ENTERED GRAMS
    // ============================================================
    public async Task HandleGramsInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[BU] HandleGramsInput '{text}'");

        if (!int.TryParse(text, out int grams) || grams <= 0)
        {
            string err = user.Language == "kz"
                ? "Дұрыс сан енгізіңіз. Мысалы: 120"
                : "Введите корректное число. Например: 120";

            await _bot.SendMessage(chatId, err, cancellationToken: ct);
            return;
        }

        var item = _foods.FirstOrDefault(f => f.Id == user.TempSelectedFoodId);

        if (item == null)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Өнім табылмады." : "Продукт не найден.",
                cancellationToken: ct);
            return;
        }

        double carbs = item.carbsPer100 / 100.0 * grams;
        double xe = carbs / 12.0;

        BotLogger.Info($"[BU] {item.Id}: {grams}g → {carbs} carbs → {xe} XE");

        user.XeHistory.Add(new XeRecord
        {
            Time = DateTime.UtcNow,
            Product = user.Language == "kz" ? item.name_kk : item.name_ru,
            Grams = grams,
            Xe = Math.Round(xe, 2)
        });

        string msg = user.Language == "kz"
            ? $"🍽 *{item.name_kk}*\nГрамм: {grams}\nКөмірсулар: {carbs:F1}\nХЕ: {xe:F2}"
            : $"🍽 *{item.name_ru}*\nГраммы: {grams}\nУглеводы: {carbs:F1}\nХЕ: {xe:F2}";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);

        user.Phase = BotPhase.MainMenu;
    }

    // ============================================================
    // SHOW HISTORY
    // ============================================================
    private async Task ShowHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[BU] ShowHistory");

        if (user.XeHistory.Count == 0)
        {
            string msg = user.Language == "kz"
                ? "ХЕ тарихы бос."
                : "История ХЕ пуста.";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        var list = user.XeHistory
            .OrderByDescending(x => x.Time)
            .Take(20)
            .ToList();

        string header = user.Language == "kz"
            ? "📄 Соңғы ХЕ жазбалары:\n\n"
            : "📄 Последние записи ХЕ:\n\n";

        string msg2 = header + string.Join("\n", list.Select(x =>
            $"{x.Time:dd.MM HH:mm} — {x.Product} ({x.Grams} г) = {x.Xe:F2} ХЕ"
        ));

        await _bot.SendMessage(chatId, msg2, cancellationToken: ct);
    }

    // ============================================================
    // CALLBACK HANDLER
    // ============================================================
    public async Task HandleCallbackAsync(UserData user, CallbackQuery q, CancellationToken ct)
    {
        string data = q.Data!;
        long chatId = q.Message!.Chat.Id;

        BotLogger.Info($"[BU] Callback '{data}'");

        if (data.StartsWith("BU_CAT|"))
        {
            string cat = data.Split('|')[1];
            await ShowProductsAsync(user, chatId, cat, ct);
            return;
        }

        if (data.StartsWith("BU_PROD|"))
        {
            string id = data.Split('|')[1];
            var item = _foods.FirstOrDefault(f => f.Id == id);

            if (item != null)
            {
                await AskGramsAsync(user, chatId, item, ct);
            }
            return;
        }

        if (data == "BU_BACK_CAT")
        {
            await ShowCategoryMenuAsync(user, chatId, ct);
            return;
        }

        if (data == "BU_BACK_MAIN")
        {
            await ShowMenuAsync(user, chatId, ct);
            return;
        }
    }
}

