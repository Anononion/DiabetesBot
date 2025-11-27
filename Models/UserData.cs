namespace DiabetesBot.Models;

public class UserData
{
    public long UserId { get; set; }

    // текущий язык
    public string Language { get; set; } = "ru";

    // текущая фаза
    public BotPhase Phase { get; set; } = BotPhase.MainMenu;

    // сохранённая история измерений
    public List<Measurement> Measurements { get; set; } = new();

    // дневник еды
    public List<FoodEntry> FoodDiary { get; set; } = new();

    // история хлебных единиц
    public List<XeRecord> XeHistory { get; set; } = new();
}
