using DiabetesBot;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);

// DI — BotService
builder.Services.AddSingleton<BotService>(sp =>
{
    string? token = Environment.GetEnvironmentVariable("BOT_TOKEN");
    if (string.IsNullOrEmpty(token))
        throw new Exception("BOT_TOKEN missing");

    return new BotService(token);
});

var app = builder.Build();

// Webhook endpoint
app.MapPost("/webhook/{token}", async (HttpContext ctx, BotService bot, string token) =>
{
    if (token != Environment.GetEnvironmentVariable("BOT_TOKEN"))
        return Results.Unauthorized();

    var update = await ctx.Request.ReadFromJsonAsync<Update>();
    if (update != null)
        await bot.HandleWebhookAsync(update);

    return Results.Ok();
});

// Register webhook on start
app.Lifetime.ApplicationStarted.Register(async () =>
{
    var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
    var bot = app.Services.GetRequiredService<BotService>();

    // ВАЖНО! URL должен совпадать с Render!
    string webhookUrl = $"https://diacare-2x9i.onrender.com/webhook/{token}";

    await bot.SetWebhookAsync(webhookUrl);
    Logger.Info($"Webhook registered: {webhookUrl}");
});

app.Run();
