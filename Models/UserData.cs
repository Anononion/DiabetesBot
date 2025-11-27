namespace DiabetesBot.Models;

public class UserData
{
    public long UserId { get; set; }

    // язык пользователя
    public string Language { get; set; } = "ru";

    // текущее состояние (фаза)
    public BotPhase Phase { get; set; } = BotPhase.MainMenu;

    // список измерений
    public List<Measurement> Measurements { get; set; } = new();

    // дневник еды
    public List<FoodEntry> FoodDiary { get; set; } = new();

    // история ХЕ
    public List<XeRecord> XeHistory { get; set; } = new();
}
