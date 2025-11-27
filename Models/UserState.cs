namespace DiabetesBot.Models;

public class UserState
{
    public long UserId { get; set; }

    // текущая фаза
    public BotPhase Phase { get; set; } = BotPhase.MainMenu;

    // выбранный язык
    public string Language { get; set; } = "ru";
}
