using Telegram.Bot;
using Telegram.Bot.Types;
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

        Logger.Info($"[BU] Продуктов загружено: {_foods.Count}");
        Logger.Info($"[BU] Категорий загружено: {_categories.Count}");
    }

    // =======================================================
    // ГЛАВНОЕ МЕНЮ ХЕ
    // =======================================================
    public async Task ShowMain(long chatId, string lang, CancellationToken ct)
    {
        string t_add = lang == "kk" ? "➕ Өнім қосу" : "➕ Добавить продукт";
        string t_hist = lang == "kk" ? "📄 ХЕ тарихы" : "📄 История ХЕ";
        string t_back = lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { t_add },
            new KeyboardButton[] { t_hist },
            new KeyboardButton[] { t_back }
        })
        { ResizeKeyboard = true };

        string msg = lang == "kk"
            ? "🥖 Нан бірліктері — әрекетті таңдаңыз:"
            : "🥖 Хлебные единицы — выберите действие:";

        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);
    }

    // =======================================================
    // ОБРАБОТКА ТЕКСТА
    // =======================================================
    public async Task HandleMessage(long chatId, string text, CancellationToken ct)
    {
        long userId = chatId;
        var user = await _storage.LoadAsync(userId);
        string lang = user.Language;

        var phase = await _state.GetPhaseAsync(userId);
        if (phase != UserPhase.BreadUnits)
            return;

        string t_add = lang == "kk" ? "➕ Өнім қосу" : "➕ Добавить продукт";
        string t_hist = lang == "kk" ? "📄 ХЕ тарихы" : "📄 История ХЕ";

        if (text == t_add)
        {
            await ShowCategoryMenu(chatId, lang, ct);
            return;
        }

        if (text == t_hist)
        {
            await ShowHistory(chatId, lang, ct);
            return;
        }
    }

    // =======================================================
    // CALLBACK – кнопки
    // =======================================================
    public async Task HandleButton(long chatId, string data, CancellationToken ct)
    {
        if (!data.StartsWith("BU_"))
            return;

        Logger.Info($"[BU] Callback: {data}");

        if (data == "BU_ADD")
        {
            var user = await _storage.LoadAsync(chatId);
            await ShowCategoryMenu(chatId, user.Language, ct);
            return;
        }

        if (data.StartsWith("BU_CAT_"))
        {
            string cat = data.Replace("BU_CAT_", "");
            var user = await _storage.LoadAsync(chatId);
            await ShowProductsInCategory(chatId, cat, user.Language, ct);
            return;
        }

        if (data.StartsWith("BU_PROD_"))
        {
            string id = data.Replace("BU_PROD_", "");
            var user = await _storage.LoadAsync(chatId);
            await AskWeight(chatId, id, user.Language, ct);
            return;
        }
    }

    // =======================================================
    // КАТЕГОРИИ
    // =======================================================
    private async Task ShowCategoryMenu(long chatId, string lang, CancellationToken ct)
    {
        Logger.Info("[BU] Открыто меню категорий");

        var rows = _categories.Keys
            .Select(c => new[]
            {
                InlineKeyboardButton.WithCallbackData(c, "BU_CAT_" + c)
            })
            .ToList();

        rows.Add(new[] {
            InlineKeyboardButton.WithCallbackData(
                lang == "kk" ? "⬅ Назад" : "⬅ Назад",
                "BU_ADD")
        });

        string msg = lang == "kk"
            ? "Категорияны таңдаңыз:"
            : "Выберите категорию:";

        await _bot.SendMessage(chatId, msg,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // =======================================================
    // ПРОДУКТЫ В КАТЕГОРИИ
    // =======================================================
    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return new string(
            s.Trim()
             .ToLowerInvariant()
             .Replace('ё', 'е')
             .Where(c => !char.IsControl(c))
             .ToArray());
    }

    private async Task ShowProductsInCategory(long chatId, string cat, string lang, CancellationToken ct)
    {
        Logger.Info($"[BU] Выбрана категория: '{cat}'");

        string normCat = Normalize(cat);
        var key = _categories.Keys.FirstOrDefault(k => Normalize(k) == normCat);

        if (key == null)
        {
            await _bot.SendMessage(chatId,
                lang == "kk"
                    ? $"Қате: *{cat}* табылмады."
                    : $"Ошибка: категория *{cat}* не найдена.",
                cancellationToken: ct);
            return;
        }

        var rawList = _categories[key];
        if (rawList == null || rawList.Count == 0)
        {
            await _bot.SendMessage(chatId,
                lang == "kk"
                    ? $"Бұл категория бос."
                    : $"Для категории нет продуктов.",
                cancellationToken: ct);
            return;
        }

        var foodsInCategory = _foods
            .Where(f => rawList.Any(r =>
                Normalize(r) == Normalize(f.Id) ||
                Normalize(r) == Normalize(f.Name)))
            .ToList();

        if (foodsInCategory.Count == 0)
        {
            await _bot.SendMessage(chatId,
                lang == "kk"
                    ? "Бұл категорияда өнімдер жоқ."
                    : "В категории нет совпадающих продуктов.",
                cancellationToken: ct);
            return;
        }

        var rows = foodsInCategory
            .Select(f => new[]
            {
                InlineKeyboardButton.WithCallbackData(f.Name, "BU_PROD_" + f.Id)
            })
            .ToList();

        rows.Add(new[] {
            InlineKeyboardButton.WithCallbackData(
                lang == "kk" ? "⬅ Назад" : "⬅ Назад",
                "BU_ADD")
        });

        string msg = lang == "kk"
            ? $"Категория: *{cat}*"
            : $"Продукты категории *{cat}*:";

        await _bot.SendMessage(chatId, msg,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // =======================================================
    // ВВОД ВЕСА
    // =======================================================
    private async Task AskWeight(long chatId, string id, string lang, CancellationToken ct)
    {
        long userId = chatId;

        _state.TempString(userId, "food_id", id);
        _state.SetStep(userId, UserStep.BU_WaitWeight);

        var item = _foods.FirstOrDefault(f => f.Id == id);

        if (item == null)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Өнім табылмады." : "Продукт не найден.",
                cancellationToken: ct);
            return;
        }

        string msg = lang == "kk"
            ? $"*{item.Name}* үшін грамм санын енгізіңіз:"
            : $"Введите вес *{item.Name}* в граммах:";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // =======================================================
    // ОБРАБОТКА ВВЕДЁННОГО ВЕСА
    // =======================================================
    public async Task HandleText(long chatId, string text, CancellationToken ct)
    {
        long userId = chatId;
        var user = await _storage.LoadAsync(userId);
        string lang = user.Language;

        if (_state.GetState(userId).State.Step != UserStep.BU_WaitWeight)
            return;

        if (!int.TryParse(text, out int grams) || grams <= 0)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Граммды дұрыс енгізіңіз." : "Введите корректное количество граммов.",
                cancellationToken: ct);
            return;
        }

        string id = _state.TempString(userId, "food_id");

        var item = _foods.FirstOrDefault(f => f.Id == id);

        if (item == null)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Өнім табылмады." : "Продукт не найден.",
                cancellationToken: ct);
            return;
        }

        double carbs = item.CarbsPer100 / 100.0 * grams;
        double xe = Math.Round(carbs / 12.0, 2);

        _storage.AppendXeRecord(userId, new XeRecord
        {
            Timestamp = DateTime.UtcNow,
            Product = item.Name,
            Grams = grams,
            Xe = xe
        });

        string msg = lang == "kk"
            ? $"🍽 *{item.Name}* — {grams} г\nКөмірсулар: {carbs:F1} г\nХЕ: {xe}"
            : $"🍽 *{item.Name}* — {grams} г\nУглеводы: {carbs:F1} г\nХЕ: {xe}";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);

        _state.Clear(userId);
        await ShowMain(chatId, lang, ct);
    }

    // =======================================================
    // ИСТОРИЯ ХЕ
    // =======================================================
    private async Task ShowHistory(long chatId, string lang, CancellationToken ct)
    {
        var list = _storage.LoadXeHistory(chatId);

        if (list.Count == 0)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Тарих бос." : "История пуста.",
                cancellationToken: ct);
            return;
        }

        string txt = lang == "kk"
            ? "📄 ХЕ тарихы:\n\n"
            : "📄 История ХЕ:\n\n";

        txt += string.Join("\n", list
            .TakeLast(20)
            .Select(r =>
                $"{r.Timestamp:dd.MM HH:mm} — {r.Product} ({r.Grams} г) = {r.Xe} ХЕ"));

        await _bot.SendMessage(chatId, txt, cancellationToken: ct);
    }
}
