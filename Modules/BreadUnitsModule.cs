using System.Globalization;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Services;

namespace DiabetesBot.Modules;

public class BreadUnitsModule
{
    private readonly ITelegramBotClient _bot;
    private readonly Dictionary<string, List<FoodItem>> _foodsByCategory;
    private readonly UserData _tempState = new(); // используется только как временная корзина

    public BreadUnitsModule(ITelegramBotClient bot)
    {
        _bot = bot;

        // Загружаем продукты
        var foods = JsonStorageService.LoadFoods() ?? new List<FoodItem>();

        _foodsByCategory = foods
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    // ------------------------------------------------------------------
    // Главное меню блока ХЕ
    // ------------------------------------------------------------------
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { user.Language == "kz" ? "Санаттар" : "Категории" },
            new KeyboardButton[] { user.Language == "kz" ? "Тарих" : "История" },
            new KeyboardButton[] { user.Language == "kz" ? "Артқа" : "Назад" }
        })
        {
            ResizeKeyboard = true
        };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Нан бірліктері мәзірі:" : "Меню хлебных единиц:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ------------------------------------------------------------------
    // Обработка текста (когда user.Phase == BreadUnits)
    // ------------------------------------------------------------------
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        if (text.Contains("Категории") || text.Contains("Санаттар"))
        {
            await SendCategoriesAsync(user, chatId, ct);
            return;
        }

        if (text.Contains("История") || text.Contains("Тарих"))
        {
            await SendHistoryAsync(user, chatId, ct);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // ------------------------------------------------------------------
    // Показ категорий с inline-кнопками
    // ------------------------------------------------------------------
    private async Task SendCategoriesAsync(UserData user, long chatId, CancellationToken ct)
    {
        var rows = _foodsByCategory.Keys
            .Select(c =>
                InlineKeyboardButton.WithCallbackData(
                    user.Language == "kz" ? c : c,
                    $"XE_CAT:{c}"))
            .Chunk(2)
            .Select(r => r.ToArray())
            .ToArray();

        var kb = new InlineKeyboardMarkup(rows);

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Санатты таңдаңыз:" : "Выберите категорию:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ------------------------------------------------------------------
    // Показ списка продуктов категории XE_CAT:CategoryName
    // ------------------------------------------------------------------
    public async Task HandleCallbackAsync(UserData user, CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Data == null)
            return;

        // Категория
        if (cb.Data.StartsWith("XE_CAT:"))
        {
            string category = cb.Data.Replace("XE_CAT:", "");
            await ShowProductsAsync(user, cb.Message!.Chat.Id, category, ct);
            return;
        }

        // Продукт
        if (cb.Data.StartsWith("XE_PROD:"))
        {
            string productId = cb.Data.Replace("XE_PROD:", "");
            await AskGramsAsync(user, cb.Message!.Chat.Id, productId, ct);
            return;
        }

        // Завершение ввода — игнор, работает в текстовой фазе
    }

    // ------------------------------------------------------------------
    // Продукты категории
    // ------------------------------------------------------------------
    private async Task ShowProductsAsync(UserData user, long chatId, string category, CancellationToken ct)
    {
        if (!_foodsByCategory.TryGetValue(category, out var list))
        {
            await _bot.SendMessage(chatId, "Ошибка: категория пуста.", cancellationToken: ct);
            return;
        }

        var buttons = list
            .Select(f =>
                InlineKeyboardButton.WithCallbackData(
                    user.Language == "kz" ? f.NameKk : f.NameRu,
                    $"XE_PROD:{f.Id}"))
            .Chunk(1)
            .Select(r => r.ToArray())
            .ToArray();

        var kb = new InlineKeyboardMarkup(buttons);

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Өнім таңдаңыз:" : "Выберите продукт:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ------------------------------------------------------------------
    // Просим граммы
    // ------------------------------------------------------------------
    private async Task AskGramsAsync(UserData user, long chatId, string productId, CancellationToken ct)
    {
        _tempState.TempProductId = productId;

        user.Phase = BotPhase.BreadUnits_EnterGrams;

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Грамм санын енгізіңіз:" : "Введите количество граммов:",
            cancellationToken: ct);
    }

    // ------------------------------------------------------------------
    // Обработка ввода граммов (когда Phase == BreadUnits_EnterGrams)
    // ------------------------------------------------------------------
    public async Task HandleGramsAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (!double.TryParse(text.Replace(",", "."),
            NumberStyles.Any, CultureInfo.InvariantCulture, out double grams))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct);
            return;
        }

        var product = JsonStorageService.LoadFoods()!
            .FirstOrDefault(f => f.Id == _tempState.TempProductId);

        if (product == null)
        {
            await _bot.SendMessage(chatId, "Ошибка: продукт не найден.", cancellationToken: ct);
            return;
        }

        double xe = grams / product.GramsPerXE;

        var record = new XeRecord
        {
            ProductId = product.Id,
            ProductName = user.Language == "kz" ? product.NameKk : product.NameRu,
            Grams = grams,
            XE = xe,
            Time = DateTime.UtcNow
        };

        user.BreadUnits.Add(record);
        StateStore.Save(user);

        await _bot.SendMessage(chatId,
            $"{record.ProductName}: {record.Grams} г → {record.XE:0.0} ХЕ\n" +
            (user.Language == "kz" ? "Сақталды!" : "Сохранено!"),
            cancellationToken: ct);

        user.Phase = BotPhase.BreadUnits;
        await ShowMenuAsync(user, chatId, ct);
    }

    // ------------------------------------------------------------------
    // История ХЕ
    // ------------------------------------------------------------------
    private async Task SendHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.BreadUnits.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Тарих бос." : "История пуста.",
                cancellationToken: ct);
            return;
        }

        var msg = string.Join("\n",
            user.BreadUnits
                .OrderByDescending(r => r.Time)
                .Take(10)
                .Select(r =>
                {
                    var t = r.Time.ToLocalTime();
                    return $"{t:dd.MM HH:mm} — {r.ProductName} — {r.Grams:0} г → {r.XE:0.0} ХЕ";
                }));

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }
}


