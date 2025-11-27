namespace DiabetesBot.Models;

public class Measurement
{
    public DateTime Time { get; set; } = DateTime.Now;

    // fasting / after / time
    public string Type { get; set; } = "fasting";

    public double Value { get; set; }
}
