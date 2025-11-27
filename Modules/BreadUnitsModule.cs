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

    // КАТЕГОРИЯ → СПИСОК ПРОДУКТОВ
    private Dictionary<string, List<FoodItem>> _foods = new();

    public BreadUnitsModule(ITelegramBotClient bot)
    {
        _bot = bot;
        Load();
    }

    // ====================================================================
    // ЗАГРУЗКА JSON (ВАРИАНТ А)
    // ====================================================================
    private void Load()
    {
        // 1. Грузим категории типа:
        // { "Фрукты": ["apple","banana"], ... }
        string catJson = File.ReadAllText("Data/food_categories.json");
        var categoryMap = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(catJson)!;

        // 2. Грузим все продукты
        string foodsJson = File.ReadAllText("Data/foods.json");
        var allFoods = JsonConvert.DeserializeObject<List<FoodItem>>(foodsJson)!;

        // 3. Преобразуем категории → FoodItem
        _foods = categoryMap.ToDictionary(
            cat => cat.Key,
            cat => allFoods.Where(f => cat.Value.Contains(f.Id)).ToList()
        );
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
            )
        );

        await _bot.SendMessage(chatId,
            "Выберите категорию:",
            replyMarkup: ik,
            cancellationToken: ct);
    }

    // ====================================================================
    // ПОКАЗАТЬ ПРОДУКТЫ В КАТЕГОРИИ
    // ====================================================================
    public async Task ShowItemsByCategoryAsync(UserData user, long chatId, string category, CancellationToken ct)
    {
        if (!_foods.TryGetValue(category, out var items))
        {
            await _bot.SendMessage(chatId, "Ошибка данных.", cancellationToken: ct);
            return;
        }

        var ik = new InlineKeyboardMarkup(
            items.Select(i =>
                InlineKeyboardButton.WithCallbackData(
                    $"{(user.Language == "kz" ? i.NameKz : i.NameRu)} ({i.CarbsPer100} г углеводов)",
                    $"xe_item:{i.Id}"
                )
            )
        );

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
        var all = _foods.Values.SelectMany(f => f);
        var item = all.FirstOrDefault(x => x.Id == itemId);

        if (item == null)
        {
            await _bot.SendMessage(chatId, "Ошибка.", cancellationToken: ct);
            return;
        }

        user.SelectedFood = item;
        user.Phase = BotPhase.BreadUnits_EnterGrams;

        string name = user.Language == "kz" ? item.NameKz : item.NameRu;

        await _bot.SendMessage(chatId,
            $"Введите граммы для '{name}':",
            cancellationToken: ct);
    }

    // ====================================================================
    // ВВОД ГРАММОВ
    // ====================================================================
    public async Task HandleGramsInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (!int.TryParse(text, out int grams))
        {
            await _bot.SendMessage(chatId, "Введите число.", cancellationToken: ct);
            return;
        }

        if (user.SelectedFood == null)
        {
            await _bot.SendMessage(chatId, "Ошибка.", cancellationToken: ct);
            return;
        }

        double xe = grams / 12.0; // УПРОЩЁННАЯ ФОРМУЛА из старой версии

        string name = user.Language == "kz" ? user.SelectedFood.NameKz : user.SelectedFood.NameRu;

        await _bot.SendMessage(chatId,
            $"{name}\n{grams} г ≈ {xe:0.0} ХЕ",
            cancellationToken: ct);

        user.Phase = BotPhase.BreadUnits;
        await ShowMenuAsync(user, chatId, ct);
    }
}
