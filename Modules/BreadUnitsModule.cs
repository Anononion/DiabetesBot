using System.Text.Json;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
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
        Load();
    }

    private void Load()
    {
        string baseDir = AppContext.BaseDirectory;

        string catPath = Path.Combine(baseDir, "Data", "food_categories.json");
        string foodsPath = Path.Combine(baseDir, "Data", "foods.json");

        _categories = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
            System.IO.File.ReadAllText(catPath))!;

        _foods = JsonSerializer.Deserialize<List<FoodItem>>(
            System.IO.File.ReadAllText(foodsPath))!;
    }


    // ============================================================
    // MAIN MENU
    // ============================================================
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        string add = user.Language == "kz" ? "➕ Өнім қосу" : "➕ Добавить продукт";
        string history = user.Language == "kz" ? "📄 ХЕ тарихы" : "📄 История ХЕ";
        string back = user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { add },
            new KeyboardButton[] { history },
            new KeyboardButton[] { back }
        }) { ResizeKeyboard = true };

        string msg = user.Language == "kz" ? "ХЕ әрекетін таңдаңыз:" : "Выберите действие по ХЕ:";
        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);
    }

    // ============================================================
    // HANDLE TEXT
    // ============================================================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
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
    // CATEGORY SELECT
    // ============================================================
    private async Task ShowCategoryMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var rows = _categories.Keys.Select(cat =>
            new[]
            {
                InlineKeyboardButton.WithCallbackData(cat, $"BU_CAT|{cat}")
            }).ToList();

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(
                user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад", "BU_BACK_MAIN")
        });

        string msg = user.Language == "kz" ? "Категория таңдаңыз:" : "Выберите категорию:";
        await _bot.SendMessage(chatId, msg, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    // ============================================================
    // PRODUCT LIST
    // ============================================================
    private async Task ShowProductsAsync(UserData user, long chatId, string cat, CancellationToken ct)
    {
        var ids = _categories[cat];
        var items = _foods.Where(f => ids.Contains(f.Id)).ToList();

        var rows = items.Select(f =>
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    user.Language == "kz" ? f.name_kk : f.name_ru,
                    $"BU_PROD|{f.Id}")
            }).ToList();

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(
                user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад", "BU_BACK_CAT")
        });

        await _bot.SendMessage(chatId, cat, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    // ============================================================
    // ENTER GRAMS
    // ============================================================
    private async Task AskGramsAsync(UserData user, long chatId, FoodItem item, CancellationToken ct)
    {
        user.TempSelectedFoodId = item.Id;
        user.Phase = BotPhase.BreadUnits_EnterGrams;

        string msg = user.Language == "kz"
            ? $"{item.name_kk} — грамм енгізіңіз:"
            : $"Введите граммы для: {item.name_ru}";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    public async Task HandleGramsInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (!int.TryParse(text, out int grams) || grams <= 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Дұрыс сан енгізіңіз." : "Введите корректное число.",
                cancellationToken: ct);
            return;
        }

        var item = _foods.First(f => f.Id == user.TempSelectedFoodId);

        double carbs = item.carbsPer100 / 100.0 * grams;
        double xe = carbs / 12.0;

        user.XeHistory.Add(new XeRecord
        {
            Time = DateTime.Now,
            Product = user.Language == "kz" ? item.name_kk : item.name_ru,
            Grams = grams,
            Xe = Math.Round(xe, 2)
        });

        string msg = user.Language == "kz"
            ? $"{item.name_kk}\n{grams} г → {xe:F2} ХЕ"
            : $"{item.name_ru}\n{grams} г → {xe:F2} ХЕ";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);

        user.Phase = BotPhase.MainMenu;
    }

    // ============================================================
    // HISTORY
    // ============================================================
    private async Task ShowHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.XeHistory.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Тарих бос." : "История пуста.",
                cancellationToken: ct);
            return;
        }

        var last = user.XeHistory.OrderByDescending(x => x.Time).Take(20);

        string msg = string.Join('\n', last.Select(x =>
            $"{x.Time:dd.MM HH:mm} — {x.Product} {x.Grams} г = {x.Xe:F2} ХЕ"
        ));

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // ============================================================
    // CALLBACK
    // ============================================================
    public async Task HandleCallbackAsync(UserData user, CallbackQuery q, CancellationToken ct)
    {
        string data = q.Data!;
        long chatId = q.Message!.Chat.Id;

        if (data.StartsWith("BU_CAT|"))
        {
            await ShowProductsAsync(user, chatId, data.Split('|')[1], ct);
            return;
        }

        if (data.StartsWith("BU_PROD|"))
        {
            string id = data.Split('|')[1];
            var item = _foods.First(f => f.Id == id);
            await AskGramsAsync(user, chatId, item, ct);
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

