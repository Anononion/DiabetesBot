using DiabetesBot;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Types;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BotService>(sp =>
{
    var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
    if (string.IsNullOrEmpty(token))
        throw new Exception("BOT_TOKEN missing");

    return new BotService(token);
});

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

var app = builder.Build();

app.MapPost("/webhook/{token}", async (HttpContext ctx, string token, BotService bot) =>
{
    if (token != Environment.GetEnvironmentVariable("BOT_TOKEN"))
        return Results.Unauthorized();

    try
    {
        var update = await JsonSerializer.DeserializeAsync<Update>(ctx.Request.Body, jsonOptions);

        if (update == null)
        {
            Logger.Error("[WEBHOOK] Update == null");
            return Results.Ok();
        }

        await bot.HandleWebhookAsync(update);
    }
    catch (Exception ex)
    {
        Logger.Error("[WEBHOOK] Exception", ex);
        return Results.Ok(); // не слать 500 telegram
    }

    return Results.Ok();
});

async Task RegisterWebhook()
{
    var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
    var bot = app.Services.GetRequiredService<BotService>();
    string url = $"https://diacare-2x9i.onrender.com/webhook/{token}";
    await bot.SetWebhookAsync(url);
}

_ = RegisterWebhook();

app.Run();
