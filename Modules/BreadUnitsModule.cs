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

    // category → list of foods
    private Dictionary<string, List<FoodItem>> _foods = new();

    public BreadUnitsModule(ITelegramBotClient bot)
    {
        _bot = bot;
        Load();
    }

    // ====================================================================
    // LOAD JSON — matches your actual structure
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

        // group by category field inside JSON
        _foods = items
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    // ====================================================================
    // MAIN MENU
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
    // SHOW CATEGORIES
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
    // SHOW ITEMS IN CATEGORY
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
                    $"{(user.Language == "kz" ? i.NameKk : i.NameRu)} ({i.GramsPerXE} г = 1 ХЕ)",
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
    // SELECT ITEM
    // ====================================================================
    public async Task SelectItemAsync(UserData user, long chatId, string itemId, CancellationToken ct)
    {
        var all = _foods.Values.SelectMany(x => x);
        var item = all.FirstOrDefault(x => x.Id == itemId);

        if (item == null)
        {
            await _bot.SendMessage(chatId, "Ошибка.", cancellationToken: ct);
            return;
        }

        user.SelectedFood = item;
        user.Phase = BotPhase.BreadUnits_EnterGrams;

        string name = user.Language == "kz" ? item.NameKk : item.NameRu;

        await _bot.SendMessage(chatId,
            $"Введите граммы для '{name}':",
            cancellationToken: ct);
    }

    // ====================================================================
    // ENTER GRAMS
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

        double xe = grams / (double)user.SelectedFood.GramsPerXE;

        string name = user.Language == "kz" ? user.SelectedFood.NameKk : user.SelectedFood.NameRu;

        await _bot.SendMessage(chatId,
            $"{name}\n{grams} г ≈ {xe:0.0} ХЕ",
            cancellationToken: ct);

        user.Phase = BotPhase.BreadUnits;
        await ShowMenuAsync(user, chatId, ct);
    }

    public async Task HandleCallbackAsync(UserData user, CallbackQuery cb, CancellationToken ct)
{
    string data = cb.Data ?? "";

    if (data.StartsWith("BU_CAT:"))
    {
        var cat = data.Split(':')[1];
        await ShowProductsAsync(user, cb.Message.Chat.Id, cat, ct);
        return;
    }

    if (data.StartsWith("BU_ITEM:"))
    {
        var id = data.Split(':')[1];
        user.TempFoodId = id;
        user.Phase = BotPhase.BreadUnits_EnterGrams;

        await _bot.SendText(cb.Message.Chat.Id,
            user.Language == "kz" ? "Грамм енгізіңіз:" : "Введите граммы:",
            ct);

        return;
    }
}
}

