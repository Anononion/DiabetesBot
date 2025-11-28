using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

using Newtonsoft.Json;
using File = System.IO.File;

using DiabetesBot.Utils;
using DiabetesBot.Models;
using DiabetesBot.Services;

namespace DiabetesBot.Modules;

public class BreadUnitsModule
{
    private readonly ITelegramBotClient _bot;

    // category → items
    private Dictionary<string, List<FoodItem>> _foods = new();

    public BreadUnitsModule(ITelegramBotClient bot)
    {
        _bot = bot;
        Load();
    }

    // ====================================================================
    // ЗАГРУЗКА JSON
    // ====================================================================
    private void Load()
    {
        string foodsPath = Path.Combine("Data", "foods.json");

        var items = JsonConvert.DeserializeObject<List<FoodItem>>(File.ReadAllText(foodsPath));
        if (items == null)
        {
            _foods = new Dictionary<string, List<FoodItem>>();
            return;
        }

        // группируем по Category (как у тебя в JSON)
        _foods = items
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    // ====================================================================
    // ГЛАВНОЕ МЕНЮ
    // ====================================================================
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { user.Language == "kz" ? "📂 Санаттар" : "📂 Категории" },
            new KeyboardButton[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" }
        })
        {
            ResizeKeyboard = true
        };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "ХЕ мәзірі:" : "Меню хлебных единиц:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ====================================================================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        if (text.Contains("Категории") || text.Contains("Санаттар"))
        {
            await ShowCategoriesAsync(chatId, ct);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // ====================================================================
    // ПОКАЗАТЬ КАТЕГОРИИ
    // ====================================================================
    public async Task ShowCategoriesAsync(long chatId, CancellationToken ct)
    {
        var ik = new InlineKeyboardMarkup(
            _foods.Keys.Select(cat =>
                InlineKeyboardButton.WithCallbackData(cat, $"xe_cat:{cat}")
            ).ToArray()
        );

        await _bot.SendMessage(chatId,
            "Выберите категорию:",
            replyMarkup: ik,
            cancellationToken: ct);
    }

    // ====================================================================
    // ПОКАЗАТЬ ПРОДУКТЫ КАТЕГОРИИ
    // ====================================================================
    public async Task ShowItemsByCategoryAsync(UserData user, long chatId, string category, CancellationToken ct)
    {
        if (!_foods.TryGetValue(category, out var items))
        {
            await _bot.SendMessage(chatId, "Ошибка данных.", cancellationToken: ct);
            return;
        }

        var buttons = items.Select(i =>
            InlineKeyboardButton.WithCallbackData(
                $"{(user.Language == "kz" ? i.NameKk : i.NameRu)} ({i.GramsPerXE} г = 1 ХЕ)",
                $"xe_item:{i.Id}"
            )
        ).ToArray();

        var ik = new InlineKeyboardMarkup(buttons);

        await _bot.SendMessage(chatId,
            $"Категория: {category}",
            replyMarkup: ik,
            cancellationToken: ct);
    }

    // ====================================================================
    // ВЫБОР ПРОДУКТА
    // ====================================================================
    public async Task SelectItemAsync(UserData user, long chatId, string itemId, CancellationToken ct)
    {
        var item = _foods.Values.SelectMany(x => x).FirstOrDefault(x => x.Id == itemId);

        if (item == null)
        {
            await _bot.SendMessage(chatId, "Ошибка.", cancellationToken: ct);
            return;
        }

        user.SelectedFood = item;
        user.Phase = BotPhase.BreadUnits_EnterGrams;

        string name = user.Language == "kz" ? item.NameKk : item.NameRu;

        await _bot.SendMessage(chatId,
            $"{name}\n1 ХЕ = {item.GramsPerXE} г.\nВведите граммы:",
            cancellationToken: ct);
    }

    // ====================================================================
    // ВВОД ГРАММ
    // ====================================================================
    public async Task HandleGramsInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (user.SelectedFood == null)
        {
            await _bot.SendMessage(chatId,
                "Сначала выберите продукт через категории.",
                cancellationToken: ct);

            user.Phase = BotPhase.BreadUnits;
            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        if (!double.TryParse(text.Replace(",", "."), out var grams))
        {
            await _bot.SendMessage(chatId, "Введите число грамм.", cancellationToken: ct);
            return;
        }

        var item = user.SelectedFood;
        double xe = grams / item.GramsPerXE;

        var record = new XeRecord
        {
            ProductId = item.Id,
            Grams = grams,
            XE = xe,
            Time = DateTime.UtcNow
        };

        user.XeHistory.Add(record);
        user.BreadUnits.Add(record);

        user.Phase = BotPhase.BreadUnits;

        string name = user.Language == "kz" ? item.NameKk : item.NameRu;

        await _bot.SendMessage(chatId,
            $"Записано: {xe:0.00} ХЕ ({grams} г {name})",
            cancellationToken: ct);

        await ShowMenuAsync(user, chatId, ct);
    }
}
