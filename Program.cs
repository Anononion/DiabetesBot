using DiabetesBot;
using DiabetesBot.Services;
using DiabetesBot.Utils;

internal class Program
{
    private static async Task Main()
    {
        Logger.Info("Starting DiabetesBot...");

        var env = new EnvConfigService();
        env.LoadAndDecryptEnv();  

        string? botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
        if (string.IsNullOrEmpty(botToken))
        {
            Logger.Error("BOT_TOKEN not found", new Exception("Missing token"));
            return;
        }

        var bot = new BotService(botToken);
        await bot.StartAsync();
    }
}
