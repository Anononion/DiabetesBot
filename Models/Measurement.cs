namespace DiabetesBot.Models;

public class Measurement
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Type { get; set; } = "fasting"; // fasting, postmeal, timed, skipped
    public double? Value { get; set; } // null если skipped
}
