using DiabetesBot;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);

// DI: регистрируем BotService как singleton
builder.Services.AddSingleton<BotService>(sp =>
{
    string? token = Environment.GetEnvironmentVariable("BOT_TOKEN");

    if (string.IsNullOrEmpty(token))
        throw new Exception("BOT_TOKEN environment variable missing");

    return new BotService(token);
});

var app = builder.Build();

// Webhook endpoint
app.MapPost("/webhook/{token}", async (HttpContext ctx, BotService bot, string token) =>
{
    // фильтрация запросов по токену
    if (token != Environment.GetEnvironmentVariable("BOT_TOKEN"))
        return Results.Unauthorized();

    var update = await ctx.Request.ReadFromJsonAsync<Update>();
    if (update != null)
        await bot.HandleWebhookAsync(update);

    return Results.Ok();
});

app.Run();
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

