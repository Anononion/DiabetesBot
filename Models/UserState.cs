namespace DiabetesBot.Models;

public class UserState
{
    public long UserId { get; set; }

    // текущая фаза диалога
    public BotPhase Phase { get; set; } = BotPhase.MainMenu;

    // язык пользователя
    public string Language { get; set; } = "ru";
}
