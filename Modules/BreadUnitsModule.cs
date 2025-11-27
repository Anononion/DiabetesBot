using Telegram.Bot;
using Telegram.Bot.Types;
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
    private Dictionary<string, List<FoodItem>> _foods = new();

    public BreadUnitsModule(ITelegramBotClient bot)
    {
        _bot = bot;

        Load();
    }

    private void Load()
    {
        string catPath = Path.Combine("Data", "food_categories.json");
        string itemsPath = Path.Combine("Data", "foods.json");

        var cats = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(catPath));
        var items = JsonConvert.DeserializeObject<List<FoodItem>>(File.ReadAllText(itemsPath));

        _foods = cats.ToDictionary(
            c => c,
            c => items.Where(x => x.Category == c).ToList()
        );
    }

    // ---------------------------------------------------------
    // Главное меню
    // ---------------------------------------------------------
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[XE] ShowMenu");

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📂 Категории" },
            new KeyboardButton[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" }
        }) { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "ХЕ мәзірі:" : "Меню хлебных единиц:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ---------------------------------------------------------
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        if (text.Contains("Категории"))
        {
            await ShowCategoriesAsync(chatId, ct);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    // Показ категорий (callback)
    // ---------------------------------------------------------
    public async Task ShowCategoriesAsync(long chatId, CancellationToken ct)
    {
        var ik = new InlineKeyboardMarkup(
            _foods.Keys.Select(c =>
                InlineKeyboardButton.WithCallbackData(c, $"xe_cat:{c}")
            )
        );

        await _bot.SendMessage(chatId, "Выберите категорию:", replyMarkup: ik, cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // Показ продуктов категории
    // ---------------------------------------------------------
    public async Task ShowItemsByCategoryAsync(UserData user, long chatId, string category, CancellationToken ct)
    {
        if (!_foods.TryGetValue(category, out var items))
        {
            await _bot.SendMessage(chatId, "Ошибка данных.", cancellationToken: ct);
            return;
        }

        var ik = new InlineKeyboardMarkup(
            items.Select(i =>
                InlineKeyboardButton.WithCallbackData($"{i.Name} ({i.GramsPerXE} г = 1 ХЕ)",
                    $"xe_item:{i.Name}")
            )
        );

        await _bot.SendMessage(chatId, $"Категория: {category}", replyMarkup: ik, cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // Выбор конкретного продукта
    // ---------------------------------------------------------
    public async Task SelectItemAsync(UserData user, long chatId, string itemName, CancellationToken ct)
    {
        var all = _foods.Values.SelectMany(x => x);
        var item = all.FirstOrDefault(x => x.Name == itemName);

        if (item == null)
        {
            await _bot.SendMessage(chatId, "Ошибка.", cancellationToken: ct);
            return;
        }

        user.SelectedFood = item;

        user.Phase = BotPhase.BreadUnits_EnterGrams;

        await _bot.SendMessage(chatId,
            $"Введите граммы для '{item.Name}':",
            cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // Ввод граммов
    // ---------------------------------------------------------
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

        await _bot.SendMessage(chatId,
            $"{user.SelectedFood.Name}\n{grams} г ≈ {xe:0.0} ХЕ",
            cancellationToken: ct);

        user.Phase = BotPhase.BreadUnits;
        await ShowMenuAsync(user, chatId, ct);
    }
}

