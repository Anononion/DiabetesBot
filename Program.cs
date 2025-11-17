using DiabetesBot;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using System.Net;

internal class Program
{
    private static async Task Main()
    {
        Logger.Info("Starting DiabetesBot...");

        var env = new EnvConfigService();
        env.LoadAndDecryptEnv();

        string? token = Environment.GetEnvironmentVariable("BOT_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            Logger.Error("BOT_TOKEN not found", new Exception("Missing token"));
            return;
        }

        string? publicUrl = Environment.GetEnvironmentVariable("PUBLIC_URL");
        if (string.IsNullOrEmpty(publicUrl))
        {
            Logger.Error("PUBLIC_URL not found", new Exception("Missing PUBLIC_URL"));
            return;
        }

        var bot = new BotService(token);

        // ===== УСТАНОВКА WEBHOOK =====
        string webhookUrl = $"{publicUrl}/webhook/{token}";
        await bot.SetWebhookAsync(webhookUrl);

        Logger.Info($"Webhook set to: {webhookUrl}");

        // ===== ЗАПУСК HTTP-LISTENER =====
        HttpListener server = new();
        server.Prefixes.Add($"{publicUrl}/webhook/{token}/");
        server.Start();

        Logger.Info("HTTP Listener started");

        while (true)
        {
            var context = await server.GetContextAsync();
            using var reader = new StreamReader(context.Request.InputStream);
            string json = await reader.ReadToEndAsync();

            var update = Newtonsoft.Json.JsonConvert.DeserializeObject<Telegram.Bot.Types.Update>(json);
            if (update != null)
                await bot.HandleUpdateAsync(update);

            context.Response.StatusCode = 200;
            context.Response.Close();
        }
    }
}
