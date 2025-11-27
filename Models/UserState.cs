using System.Collections.Generic;

namespace DiabetesBot.Models;

public class UserState
{
    public UserStep Step { get; set; } = UserStep.None;

    public Dictionary<string, string> Temp { get; set; } = new();
}
