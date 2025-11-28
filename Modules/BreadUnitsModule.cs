using DiabetesBot.Models;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Modules;

public class BreadUnitsModule
{
    private readonly ITelegramBotClient _bot;
    private readonly List<FoodCategory> _categories;
    private readonly List<FoodItem> _foods;

    public BreadUnitsModule(ITelegramBotClient bot,
        List<FoodCategory> categories,
        List<FoodItem> foods)
    {
        _bot = bot;
        _categories = categories;
        _foods = foods;
    }

    // ----------------------------------------------------
    // Главное меню XE
    // ----------------------------------------------------
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📂 Категории продуктов" },
            new KeyboardButton[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" }
        })
        {
            ResizeKeyboard = true
        };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Нан бірліктері (XE):" : "Хлебные единицы (XE):",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ----------------------------------------------------
    // Текстовый ввод
    // ----------------------------------------------------
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        if (text.Contains("Категории"))
        {
            await ShowCategoriesAsync(chatId, ct, user);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // ----------------------------------------------------
    // КАТЕГОРИИ
    // ----------------------------------------------------
    private async Task ShowCategoriesAsync(long chatId, CancellationToken ct, UserData user)
    {
        var rows = _categories
            .Select(cat => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    cat.NameRu,
                    $"XE_CAT:{cat.Id}"
                )
            })
            .ToArray();

        var kb = new InlineKeyboardMarkup(rows);

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Санатты таңдаңыз:" : "Выберите категорию:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ----------------------------------------------------
    // ОБРАБОТКА КНОПОК
    // ----------------------------------------------------
    public async Task HandleCallbackAsync(UserData user, CallbackQuery cb, CancellationToken ct)
    {
        string data = cb.Data!;

        // КАТЕГОРИЯ
        if (data.StartsWith("XE_CAT:"))
        {
            string id = data.Substring("XE_CAT:".Length);

            var items = _foods.Where(x => x.Category == id).ToList();

            var rows = items
                .Select(f => new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        $"{f.NameRu} ({f.GramsPerXE} г = 1 XE)",
                        $"XE_ITEM:{f.Id}"
                    )
                })
                .ToArray();

            var kb = new InlineKeyboardMarkup(rows);

            await _bot.EditMessageText(
                cb.Message!.Chat.Id,
                cb.Message.MessageId,
                $"Выберите продукт (категория {id}):",
                replyMarkup: kb,
                cancellationToken: ct);

            return;
        }

        // ПРОДУКТ
        if (data.StartsWith("XE_ITEM:"))
        {
            string id = data.Substring("XE_ITEM:".Length);

            var item = _foods.FirstOrDefault(x => x.Id == id);
            if (item == null) return;

            user.LastXE_Product = id;
            user.Phase = BotPhase.BreadUnits_EnterGrams;

            await _bot.SendMessage(cb.Message!.Chat.Id,
                $"Введите граммы для {item.NameRu} (1 XE = {item.GramsPerXE} г)",
                cancellationToken: ct);

            return;
        }
    }

    // ----------------------------------------------------
    // ВВОД ГРАММ
    // ----------------------------------------------------
    public async Task HandleGramsInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (!double.TryParse(text.Replace(",", "."), out double grams))
        {
            await _bot.SendMessage(chatId,
                "Введите число грамм",
                cancellationToken: ct);
            return;
        }

        var item = _foods.FirstOrDefault(x => x.Id == user.LastXE_Product);
        if (item == null) return;

        double xe = grams / item.GramsPerXE;

        user.XeHistory.Add(new XeRecord
        {
            ProductId = item.Id,
            Grams = grams,
            XE = xe,
            Time = DateTime.UtcNow
        });

        user.Phase = BotPhase.BreadUnits;

        await _bot.SendMessage(chatId,
            $"Записано: {xe:0.00} XE ({grams} г {item.NameRu})",
            cancellationToken: ct);

        await ShowMenuAsync(user, chatId, ct);
    }
}
