namespace DiabetesBot.Models;

public class FoodEntry
{
    public string Name { get; set; } = string.Empty;
    public double Grams { get; set; }
    public double BreadUnits { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}
