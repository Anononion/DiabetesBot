using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
using DiabetesBot.Services;
using DiabetesBot.Utils;

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

    // ============================================================
    // Главное меню
    // ============================================================
    public async Task ShowMain(long chatId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        string lang = user.Language;

        string add = lang == "kk" ? "➕ Өнім қосу" : "➕ Добавить продукт";
        string history = lang == "kk" ? "📄 ХЕ тарихы" : "📄 История ХЕ";
        string back = lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню";
        string title = lang == "kk" ? "🥖 Нан бірліктері — әрекетті таңдаңыз:" :
                                      "🥖 Хлебные единицы — выберите действие:";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { add },
            new KeyboardButton[] { history },
            new KeyboardButton[] { back }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId, title, replyMarkup: kb, cancellationToken: ct);
    }

    public async Task HandleMessage(long chatId, string text, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        string lang = user.Language;

        string add = lang == "kk" ? "➕ Өнім қосу" : "➕ Добавить продукт";
        string history = lang == "kk" ? "📄 ХЕ тарихы" : "📄 История ХЕ";

        long userId = chatId;
        var phase = await _state.GetPhaseAsync(userId);
        if (phase != UserPhase.BreadUnits) return;

        if (text == add)
        {
            await ShowCategoryMenu(chatId, ct);
            return;
        }

        if (text == history)
        {
            await ShowHistory(chatId, ct);
            return;
        }
    }

    // ============================================================
    // Меню категорий
    // ============================================================
    private async Task ShowCategoryMenu(long chatId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        string lang = user.Language;

        string back = lang == "kk" ? "⬅ Артқа" : "⬅ Назад";
        string title = lang == "kk" ? "Санатты таңдаңыз:" : "Выберите категорию:";

        var rows = _categories.Keys
            .Select(c => new[]
            {
                InlineKeyboardButton.WithCallbackData(c, "BU_CAT_" + c)
            })
            .ToList();

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(back, "BU_ADD") });

        await _bot.SendMessage(chatId,
            title,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // ============================================================
    // Продукты в категории
    // ============================================================
    private async Task ShowProductsInCategory(long chatId, string cat, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        string lang = user.Language;

        string back = lang == "kk" ? "⬅ Артқа" : "⬅ Назад";
        string title = lang == "kk" ? $"Категория: *{cat}*" : $"Категория *{cat}*:";

        string normCat = Normalize(cat);
        var key = _categories.Keys.FirstOrDefault(k => Normalize(k) == normCat);

        if (key == null)
        {
            string err = lang == "kk"
                ? $"Қате: *{cat}* санаты табылмады."
                : $"Ошибка: категория *{cat}* не найдена.";

            await _bot.SendMessage(chatId, err, cancellationToken: ct);
            return;
        }

        var rawList = _categories[key];
        var foodsInCategory = _foods
            .Where(f =>
                rawList.Any(r =>
                    Normalize(r) == Normalize(f.Id) ||
                    Normalize(r) == Normalize(f.Name)))
            .ToList();

        if (foodsInCategory.Count == 0)
        {
            string msg = lang == "kk"
                ? $"Бұл санатта өнімдер жоқ."
                : $"В этой категории нет продуктов.";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        var rows = foodsInCategory
            .Select(f => new[]
            {
                InlineKeyboardButton.WithCallbackData(f.Name, "BU_PROD_" + f.Id)
            })
            .ToList();

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(back, "BU_ADD") });

        await _bot.SendMessage(chatId, title,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // Нормализация
    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return new string(
            s.Trim()
             .ToLowerInvariant()
             .Replace('ё', 'е')
             .Where(c => !char.IsControl(c))
             .ToArray()
        );
    }

    // ============================================================
    // Запрос веса продукта
    // ============================================================
    private async Task AskWeight(long chatId, string id, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        string lang = user.Language;

        long userId = chatId;

        _state.TempString(userId, "food_id", id);
        _state.SetStep(userId, UserStep.BU_WaitWeight);

        var item = _foods.FirstOrDefault(f => f.Id == id)
                   ?? _foods.FirstOrDefault(f =>
                        Normalize(f.Name) == Normalize(id));

        if (item == null)
        {
            string msg = lang == "kk"
                ? "Қате: өнім табылмады."
                : "Ошибка: продукт не найден.";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        string ask = lang == "kk"
            ? $"*{item.Name}* өнімінің грамын енгізіңіз:"
            : $"Введите вес *{item.Name}* в граммах:";

        await _bot.SendMessage(chatId, ask, cancellationToken: ct);
    }

    // ============================================================
    // Обработка введённого веса
    // ============================================================
    public async Task HandleText(long chatId, string text, CancellationToken ct)
    {
        long userId = chatId;
        var user = await _storage.LoadAsync(chatId);
        string lang = user.Language;

        var phase = await _state.GetPhaseAsync(userId);

        if (phase != UserPhase.BreadUnits) return;
        if (_state.GetState(userId).State.Step != UserStep.BU_WaitWeight) return;

        if (!int.TryParse(text, out int grams) || grams <= 0)
        {
            string msg = lang == "kk"
                ? "Граммды дұрыс енгізіңіз. Мысалы: 150"
                : "Введите корректное число граммов. Например: 150.";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        string id = _state.TempString(userId, "food_id");
        var item = _foods.FirstOrDefault(f => f.Id == id)
                   ?? _foods.FirstOrDefault(f =>
                        Normalize(f.Name) == Normalize(id));

        if (item == null)
        {
            string msg = lang == "kk"
                ? "Қате: өнім табылмады."
                : "Ошибка: продукт не найден.";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        double carbs = item.CarbsPer100 / 100.0 * grams;
        double xe = carbs / 12.0;

        _storage.AppendXeRecord(userId, new XeRecord
        {
            Timestamp = DateTime.UtcNow,
            Product = item.Name,
            Grams = grams,
            Xe = Math.Round(xe, 2)
        });

        string result = lang == "kk"
            ? $"🍽 *{item.Name}* — {grams} г\nКөмірсулар: {carbs:F1} г\nХЕ: {xe:F2}"
            : $"🍽 *{item.Name}* — {grams} г\nУглеводы: {carbs:F1} г\nХЕ: {xe:F2}";

        await _bot.SendMessage(chatId, result, cancellationToken: ct);

        _state.Clear(userId);
        await ShowMain(chatId, ct);
    }

    // ============================================================
    // История ХЕ
    // ============================================================
    private async Task ShowHistory(long chatId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        string lang = user.Language;

        var list = _storage.LoadXeHistory(chatId);

        if (list.Count == 0)
        {
            string msg = lang == "kk"
                ? "Тарих бос."
                : "История пуста.";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        string title = lang == "kk" ? "📄 ХЕ тарихы:\n\n" : "📄 История ХЕ:\n\n";

        string txt = title +
                     string.Join("\n",
                         list.TakeLast(20).Select(r =>
                             $"{r.Timestamp:dd.MM HH:mm} — {r.Product} ({r.Grams} г) = {r.Xe} ХЕ"));

        await _bot.SendMessage(chatId, txt, cancellationToken: ct);
    }
}
