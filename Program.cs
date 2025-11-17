using DiabetesBot;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Types;
using Newtonsoft.Json;

internal class Program
{
    public static async Task Main(string[] args)
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

        var botService = new BotService(botToken);

        // ===== ASP.NET Webhook server =====
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        // Healthcheck для Render
        app.MapGet("/", () => "OK");

        // Webhook endpoint
        app.MapPost("/webhook/{token}", async (HttpRequest request, string token) =>
        {
            if (token != botToken)
                return Results.Unauthorized();

            using var reader = new StreamReader(request.Body);
            string body = await reader.ReadToEndAsync();

            var update = JsonConvert.DeserializeObject<Update>(body);
            if (update != null)
                await botService.ProcessUpdate(update);

            return Results.Ok();
        });

        // Устанавливаем вебхук
        string webhookUrl = $"https://{Environment.GetEnvironmentVariable("RENDER_EXTERNAL_HOSTNAME")}/webhook/{botToken}";

        await botService.SetWebhookAsync(webhookUrl);

        await app.RunAsync();
    }
}
