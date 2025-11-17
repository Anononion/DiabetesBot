using DiabetesBot;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Types;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BotService>(sp =>
{
    string? token = Environment.GetEnvironmentVariable("BOT_TOKEN");
    if (string.IsNullOrEmpty(token))
        throw new Exception("BOT_TOKEN missing");

    return new BotService(token);
});

// System.Text.Json options for Telegram updates
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,  // важно!
    PropertyNameCaseInsensitive = true,
};

var app = builder.Build();

app.MapPost("/webhook/{token}", async (HttpContext ctx, BotService bot, string token) =>
{
    if (token != Environment.GetEnvironmentVariable("BOT_TOKEN"))
        return Results.Unauthorized();

    using var reader = new StreamReader(ctx.Request.Body);
    string raw = await reader.ReadToEndAsync();

    Update? update = JsonSerializer.Deserialize<Update>(raw, jsonOptions);

    if (update != null)
        await bot.HandleWebhookAsync(update);
    else
        Logger.Warn("[WEBHOOK] Update == null — parse error");

    return Results.Ok();
});

async Task RegisterWebhook()
{
    var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
    var bot = app.Services.GetRequiredService<BotService>();
    string url = $"https://diacare-2x9i.onrender.com/webhook/{token}";

    await bot.SetWebhookAsync(url);
    Logger.Info($"Webhook set: {url}");
}

_ = RegisterWebhook();

app.Run();
