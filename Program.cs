using DiabetesBot;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);

// Загружаем .env (если нужен)
var env = new EnvConfigService();
env.LoadAndDecryptEnv();

// токен
string? botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
if (string.IsNullOrEmpty(botToken))
    throw new Exception("BOT_TOKEN environment variable missing");

// Регистрируем BotService как singleton
builder.Services.AddSingleton(new BotService(botToken));

var app = builder.Build();

// ===============================
// Webhook Endpoint
// ===============================
app.MapPost("/webhook/{token}", async (HttpContext ctx, BotService bot, string token) =>
{
    string? realToken = Environment.GetEnvironmentVariable("BOT_TOKEN");

    if (string.IsNullOrEmpty(realToken) || token != realToken)
        return Results.Unauthorized();

    Update? update = await ctx.Request.ReadFromJsonAsync<Update>();
    if (update != null)
        await bot.HandleWebhookAsync(update);

    return Results.Ok();
});

// ===============================
// Запуск приложения
// ===============================
app.Run();
