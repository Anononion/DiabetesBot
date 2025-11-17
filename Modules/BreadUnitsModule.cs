using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
using DiabetesBot.Services;
using DiabetesBot.Utils; // ЛОГГЕР работает отсюда

namespace DiabetesBot.Modules;

public class BreadUnitsModule
{
    private readonly TelegramBotClient _bot;
    private readonly UserStateService _state;
    private readonly JsonStorageService _storage;

    private readonly List<FoodItem> _foods = new();
    private readonly Dictionary<string, List<string>> _categories = new();

    public BreadUnitsModule(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage)
    {
        _bot = bot;
        _state = state;
        _storage = storage;

        _foods = _storage.LoadFoodItems();
        _categories = _storage.LoadFoodCategories();

        Logger.Info($"[BU] Загружено продуктов: {_foods.Count}");
        Logger.Info($"[BU] Загружено категорий: {_categories.Count}");
    }

    // ------------------------------------------------------
    // Главное меню
    // ------------------------------------------------------
    public async Task ShowMain(long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "➕ Добавить продукт" },
            new KeyboardButton[] { "📄 История ХЕ" },
            new KeyboardButton[] { "⬅️ В меню" }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            "🥖 Хлебные единицы — выбор действия:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    public async Task HandleMessage(long chatId, string text, CancellationToken ct)
    {
        long userId = chatId;
        var phase = await _state.GetPhaseAsync(userId);

        if (phase != UserPhase.BreadUnits)
            return;

        switch (text)
        {
            case "➕ Добавить продукт":
                await ShowCategoryMenu(chatId, ct);
                return;

            case "📄 История ХЕ":
                await ShowHistory(chatId, ct);
                return;
        }
    }

    // ------------------------------------------------------
    // Callback-кнопки
    // ------------------------------------------------------
    public async Task HandleButton(long chatId, string data, CancellationToken ct)
    {
        if (!data.StartsWith("BU_"))
            return;

        Logger.Info($"[BU] Click: {data}");

        if (data == "BU_ADD")
        {
            await ShowCategoryMenu(chatId, ct);
            return;
        }

        if (data.StartsWith("BU_CAT_"))
        {
            string cat = data.Replace("BU_CAT_", "");
            await ShowProductsInCategory(chatId, cat, ct);
            return;
        }

        if (data.StartsWith("BU_PROD_"))
        {
            string id = data.Replace("BU_PROD_", "");
            await AskWeight(chatId, id, ct);
            return;
        }
    }

    private string Norm(string s)
    {
        if (s == null) return "";

        return new string(
            s.Replace("\uFEFF", "")  // убираем BOM
             .Trim()
             .ToLower()
             .Replace("ё", "е")
             .Where(c => !char.IsControl(c))
             .ToArray()
        );
    }


    // ------------------------------------------------------
    // Выбор категории
    // ------------------------------------------------------
    private async Task ShowCategoryMenu(long chatId, CancellationToken ct)
    {
        Logger.Info("[BU] Открыто меню категорий");

        var rows = _categories.Keys
            .Select(c => new[]
            {
                InlineKeyboardButton.WithCallbackData(c, "BU_CAT_" + c)
            })
            .ToList();

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅ Назад", "BU_ADD") });

        await _bot.SendMessage(chatId,
            "Выберите категорию:",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // ---------- общий нормализатор ----------
    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        return new string(
            s.Trim()
             .ToLowerInvariant()
             .Replace('ё', 'е')
             .Where(c => !char.IsControl(c))
             .ToArray()
        );
    }
    // ------------------------------------------------------
    // Список продуктов в категории
    // ------------------------------------------------------
    private async Task ShowProductsInCategory(long chatId, string cat, CancellationToken ct)
    {
        Logger.Info($"[BU] Выбрана категория (сырая): '{cat}'");
        Logger.Info($"[BU] Ключи категорий: {string.Join(" | ", _categories.Keys)}");

        string normCat = Normalize(cat);

        // ищем реальный ключ в словаре
        var key = _categories.Keys.FirstOrDefault(k => Normalize(k) == normCat);
        Logger.Info($"[BU] Нормализованный cat='{normCat}', найденный ключ='{key}'");

        if (key is null)
        {
            Logger.Error($"[BU] Категория не найдена: '{cat}' / '{normCat}'");
            await _bot.SendMessage(chatId,
                $"Ошибка: категория *{cat}* не найдена.",
                cancellationToken: ct);
            return;
        }

        var rawList = _categories[key];
        Logger.Info($"[BU] rawList.Count для '{key}': {rawList?.Count ?? 0}");

        if (rawList == null || rawList.Count == 0)
        {
            Logger.Warn($"[BU] Строка категории '{key}' пустая");
            await _bot.SendMessage(chatId,
                $"Для категории *{cat}* нет продуктов.",
                cancellationToken: ct);
            return;
        }

        // Логируем все значения из категории
        foreach (var r in rawList)
        {
            Logger.Info($"[BU] raw item: '{r}' => '{Normalize(r)}'");
        }

        // Логируем все продукты из foods.json
        foreach (var f in _foods)
        {
            Logger.Info($"[BU] food: id='{f.Id}' -> '{Normalize(f.Id)}', " +
                        $"name='{f.Name}' -> '{Normalize(f.Name)}'");
        }

        Logger.Info("[BU] Начинаем сопоставление с foods.json");

        var foodsInCategory = _foods
            .Where(f =>
                rawList.Any(r =>
                    Normalize(r) == Normalize(f.Id) ||
                    Normalize(r) == Normalize(f.Name)))
            .ToList();

        Logger.Info($"[BU] Найдено совпадений после сравнения: {foodsInCategory.Count}");

        if (foodsInCategory.Count == 0)
        {
            Logger.Warn($"[BU] В категории {cat} НЕТ продуктов в foods.json");
            await _bot.SendMessage(chatId,
                $"Для категории *{cat}* не найдено продуктов в foods.json.",
                cancellationToken: ct);
            return;
        }

        var rows = foodsInCategory
            .Select(f => new[]
            {
                InlineKeyboardButton.WithCallbackData(f.Name, "BU_PROD_" + f.Id)
            })
            .ToList();

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅ Назад", "BU_ADD") });

        await _bot.SendMessage(
            chatId,
            $"Продукты категории *{cat}*: ",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // ------------------------------------------------------
    // Запрос веса
    // ------------------------------------------------------
    private async Task AskWeight(long chatId, string id, CancellationToken ct)
    {
        Logger.Info($"[BU] AskWeight получил id={id}");

        long userId = chatId;

        _state.TempString(userId, "food_id", id);
        _state.SetStep(userId, UserStep.BU_WaitWeight);

        var item = _foods.FirstOrDefault(f => f.Id == id);

        if (item == null)
        {
            Logger.Warn($"[BU] НЕ найден продукт по id='{id}', пробуем по имени…");

            item = _foods.FirstOrDefault(f =>
                f.Name.Trim().ToLower().Replace("ё", "е") ==
                id.Trim().ToLower().Replace("ё", "е"));
        }

        if (item == null)
        {
            Logger.Error($"[BU] Продукт НЕ найден по id И не найден по имени: {id}");
            await _bot.SendMessage(chatId, "Ошибка: продукт не найден.", cancellationToken: ct);
            return;
        }

        Logger.Info($"[BU] Найден продукт: {item.Id} / {item.Name}");

        await _bot.SendMessage(chatId,
            $"Введите вес *{item.Name}* в граммах:",
            cancellationToken: ct);
    }

    // ------------------------------------------------------
    // Обработка числа граммов
    // ------------------------------------------------------
    public async Task HandleText(long chatId, string text, CancellationToken ct)
    {
        long userId = chatId;

        var phase = await _state.GetPhaseAsync(userId);
        if (phase != UserPhase.BreadUnits)
            return;

        if (_state.GetState(userId).State.Step != UserStep.BU_WaitWeight)
            return;

        Logger.Info($"[BU] HandleText: введено '{text}'");

        if (!int.TryParse(text, out int grams) || grams <= 0)
        {
            Logger.Warn("[BU] Некорректное число граммов");
            await _bot.SendMessage(chatId, "Введите число граммов, например 150.", cancellationToken: ct);
            return;
        }

        string id = _state.TempString(userId, "food_id");

        Logger.Info($"[BU] HandleText: id из state: '{id}'");

        var item = _foods.FirstOrDefault(f => f.Id == id);

        if (item == null)
        {
            Logger.Warn("[BU] НЕ найден по id, пробуем искать по имени…");

            item = _foods.FirstOrDefault(f =>
                f.Name.Trim().ToLower().Replace("ё", "е") ==
                id.Trim().ToLower().Replace("ё", "е"));
        }

        if (item == null)
        {
            Logger.Error("[BU] HandleText: продукт НЕ НАЙДЕН");
            await _bot.SendMessage(chatId, "Ошибка: продукт не найден.", cancellationToken: ct);
            return;
        }

        Logger.Info($"[BU] Расчёт ХЕ: {item.Name}, {grams} г");

        double carbs = item.CarbsPer100 / 100.0 * grams;
        double xe = carbs / 12.0;

        _storage.AppendXeRecord(userId, new XeRecord
        {
            Timestamp = DateTime.UtcNow,
            Product = item.Name,
            Grams = grams,
            Xe = Math.Round(xe, 2)
        });

        await _bot.SendMessage(chatId,
            $"🍽 *{item.Name}* — {grams} г\nУглеводы: {carbs:F1} г\nХЕ: {xe:F2}",
            cancellationToken: ct);

        _state.Clear(userId);
        await ShowMain(chatId, ct);
    }

    // ------------------------------------------------------
    // История ХЕ
    // ------------------------------------------------------
    private async Task ShowHistory(long chatId, CancellationToken ct)
    {
        var list = _storage.LoadXeHistory(chatId);

        if (list.Count == 0)
        {
            await _bot.SendMessage(chatId, "История пуста.", cancellationToken: ct);
            return;
        }

        string txt = "📄 История ХЕ:\n\n" +
                     string.Join("\n", list
                         .TakeLast(20)
                         .Select(r => $"{r.Timestamp:dd.MM HH:mm} — {r.Product} ({r.Grams} г) = {r.Xe} ХЕ"));

        await _bot.SendMessage(chatId, txt, cancellationToken: ct);
    }
}
