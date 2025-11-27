namespace DiabetesBot.Models;

public class Lesson
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Pages { get; set; } = new();
}
