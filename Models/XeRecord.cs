namespace DiabetesBot.Models;

public class XeRecord
{
    public DateTime Timestamp { get; set; }
    public string Product { get; set; } = "";
    public int Grams { get; set; }
    public double Xe { get; set; }
}
