namespace DiabetesBot.Models;

public class UserData
{
    public long UserId { get; set; }
    public string Language { get; set; } = "ru";

    public UserPhase Phase { get; set; } = UserPhase.New;

    public UserState State { get; set; } = new();

    public List<Measurement> Measurements { get; set; } = new();

    public List<FoodEntry> FoodDiary { get; set; } = new();

    public List<XeRecord> XeHistory { get; set; } = new();
}
