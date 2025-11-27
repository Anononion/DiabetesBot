namespace DiabetesBot.Models;

public class XeRecord
{
    public DateTime Time { get; set; } = DateTime.Now;

    public string Product { get; set; } = "";
    public int Grams { get; set; }

    public double Xe { get; set; }
}
