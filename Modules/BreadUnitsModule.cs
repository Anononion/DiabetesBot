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

    // ------------------------------------------------------
    // Главное меню
    // ------------------------------------------------------
    public async Task ShowMain(long chatId, string lang, CancellationToken ct)
    {
        string add = lang == "kk" ? "➕ Өнім қосу" : "➕ Добавить продукт";
        string history = lang == "kk" ? "📄 ХЕ тарихы" : "📄 История ХЕ";
        string back = lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { add },
            new KeyboardButton[] { history },
            new KeyboardButton[] { back }
        })
        { ResizeKeyboard = true };

        string text = lang == "kk"
            ? "🥖 Нан бірліктері — әрекетті таңдаңыз:"
            : "🥖 Хлебные единицы — выберите действие:";

        await _bot.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }

    // ------------------------------------------------------
    // Обработка текстов
    // ------------------------------------------------------
    public async Task HandleMessage(long chatId, string text, string lang, CancellationToken ct)
    {
        long userId = chatId;
        var phase = await _state.GetPhaseAsync(userId);

        if (phase != UserPhase.BreadUnits)
            return;

        if (text == (lang == "kk" ? "➕ Өнім қосу" : "➕ Добавить продукт"))
        {
            await ShowCategoryMenu(chatId, lang, ct);
            return;
        }

        if (text == (lang == "kk" ? "📄 ХЕ тарихы" : "📄 История ХЕ"))
        {
            await ShowHistory(chatId, lang, ct);
            return;
        }
    }

    // ------------------------------------------------------
    // Обработка callback-кнопок
    // ------------------------------------------------------
    public async Task HandleButton(long chatId, string data, CancellationToken ct)
    {
        if (!data.StartsWith("BU_"))
            return;

        Logger.Info($"[BU] Click: {data}");

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

    // ------------------------------------------------------
    // Нормализация строк
    // ------------------------------------------------------
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
    // Выбор категории
    // ------------------------------------------------------
    private async Task ShowCategoryMenu(long chatId, string lang, CancellationToken ct)
    {
        Logger.Info("[BU] Открыто меню категорий");

        var rows = _categories.Keys
            .Select(c => new[]
            {
                InlineKeyboardButton.WithCallbackData(c, "BU_CAT_" + c)
            })
            .ToList();

        string back = lang == "kk" ? "⬅️ Артқа" : "⬅ Назад";
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(back, "BU_ADD") });

        string text = lang == "kk" ? "Санатты таңдаңыз:" : "Выберите категорию:";

        await _bot.SendMessage(chatId, text,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // ------------------------------------------------------
    // Список продуктов категории
    // ------------------------------------------------------
    private async Task ShowProductsInCategory(long chatId, string cat, string lang, CancellationToken ct)
    {
        Logger.Info($"[BU] Выбрана категория: '{cat}'");

        string normCat = Normalize(cat);
        var key = _categories.Keys.FirstOrDefault(k => Normalize(k) == normCat);

        if (key is null)
        {
            string err = lang == "kk"
                ? $"Қате: '{cat}' санаты табылмады."
                : $"Ошибка: категория '{cat}' не найдена.";

            await _bot.SendMessage(chatId, err, cancellationToken: ct);
            return;
        }

        var rawList = _categories[key];
        if (rawList.Count == 0)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Бұл санатта өнім жоқ." : "Для категории нет продуктов.",
                cancellationToken: ct);
            return;
        }

        var foodsInCategory = _foods
            .Where(f =>
                rawList.Any(r =>
                    Normalize(r) == Normalize(f.Id) ||
                    Normalize(r) == Normalize(f.Name)))
            .ToList();

        if (foodsInCategory.Count == 0)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Бұл санатта өнім табылмады." : "Нет продуктов в этой категории.",
                cancellationToken: ct);
            return;
        }

        var rows = foodsInCategory
            .Select(f => new[]
            {
                InlineKeyboardButton.WithCallbackData(f.Name, "BU_PROD_" + f.Id)
            })
            .ToList();

        string back = lang == "kk" ? "⬅️ Артқа" : "⬅ Назад";
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(back, "BU_ADD") });

        string header = lang == "kk"
            ? $"Санат: *{cat}*"
            : $"Продукты категории *{cat}*:";

        await _bot.SendMessage(chatId, header,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    // ------------------------------------------------------
    // Запрос веса
    // ------------------------------------------------------
    private async Task AskWeight(long chatId, string id, string lang, CancellationToken ct)
    {
        Logger.Info($"[BU] AskWeight id={id}");

        long userId = chatId;
        _state.TempString(userId, "food_id", id);
        _state.SetStep(userId, UserStep.BU_WaitWeight);

        var item = _foods.FirstOrDefault(f => f.Id == id);

        if (item == null)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Қате: өнім табылмады." : "Ошибка: продукт не найден.",
                cancellationToken: ct);
            return;
        }

        string text = lang == "kk"
            ? $"*{item.Name}* өнімінің грамм салығын енгізіңіз:"
            : $"Введите вес продукта *{item.Name}* в граммах:";

        await _bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    // ------------------------------------------------------
    // Обработка граммов
    // ------------------------------------------------------
    public async Task HandleText(long chatId, string text, string lang, CancellationToken ct)
    {
        long userId = chatId;

        var phase = await _state.GetPhaseAsync(userId);
        if (phase != UserPhase.BreadUnits)
            return;

        if (_state.GetState(userId).State.Step != UserStep.BU_WaitWeight)
            return;

        if (!int.TryParse(text, out int grams) || grams <= 0)
        {
            string err = lang == "kk"
                ? "Дұрыс грамм санын енгізіңіз. Мысалы: 150"
                : "Введите корректное число граммов, например 150.";

            await _bot.SendMessage(chatId, err, cancellationToken: ct);
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
        double xe = carbs / 12.0;

        _storage.AppendXeRecord(userId, new XeRecord
        {
            Timestamp = DateTime.UtcNow,
            Product = item.Name,
            Grams = grams,
            Xe = Math.Round(xe, 2)
        });

        string reply = lang == "kk"
            ? $"🍽 *{item.Name}* — {grams} г\nКөмірсулар: {carbs:F1} г\nХЕ: {xe:F2}"
            : $"🍽 *{item.Name}* — {grams} г\nУглеводы: {carbs:F1} г\nХЕ: {xe:F2}";

        await _bot.SendMessage(chatId, reply, cancellationToken: ct);

        _state.Clear(userId);
        await ShowMain(chatId, lang, ct);
    }

    // ------------------------------------------------------
    // История ХЕ
    // ------------------------------------------------------
    private async Task ShowHistory(long chatId, string lang, CancellationToken ct)
    {
        var list = _storage.LoadXeHistory(chatId);

        if (list.Count == 0)
        {
            string txt = lang == "kk" ? "Тарих бос." : "История пуста.";
            await _bot.SendMessage(chatId, txt, cancellationToken: ct);
            return;
        }

        string header = lang == "kk" ? "📄 ХЕ тарихы:\n\n" : "📄 История ХЕ:\n\n";

        string txt2 = header +
                      string.Join("\n", list
                          .TakeLast(20)
                          .Select(r => $"{r.Timestamp:dd.MM HH:mm} — {r.Product} ({r.Grams} г) = {r.Xe} ХЕ"));

        await _bot.SendMessage(chatId, txt2, cancellationToken: ct);
    }
}
