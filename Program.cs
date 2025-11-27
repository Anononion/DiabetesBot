using DiabetesBot;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Types;
using System.Text.Json;
using DiabetesBot.Utils;

var builder = WebApplication.CreateBuilder(args);

// =============== SERVICES ======================
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

// =============== PORT ===========================
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{port}");

app.UseRouting();

// =============== WEBHOOK ENDPOINT ===============
app.MapPost("/webhook/{token}", async (HttpContext ctx, string token, BotService bot) =>
{
    var expected = Environment.GetEnvironmentVariable("BOT_TOKEN");

    if (token != expected)
        return Results.Unauthorized();

    try
    {
        var update = await JsonSerializer.DeserializeAsync<Update>(ctx.Request.Body, jsonOptions);

        if (update == null)
        {
            BotLogger.Error("[WEBHOOK] update == null");
            return Results.Ok();
        }

        await bot.HandleWebhookAsync(update);
    }
    catch (Exception ex)
    {
        BotLogger.Error("[WEBHOOK] Exception", ex);
    }

    return Results.Ok();
});

// =============== SET WEBHOOK =====================
var botService = app.Services.GetRequiredService<BotService>();

// Render всегда даёт переменную RENDER_EXTERNAL_URL
var externalUrl = Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL")
    ?? "diacare-2x9i.onrender.com";

var webhookUrl =
    $"https://{externalUrl}/webhook/{Environment.GetEnvironmentVariable("BOT_TOKEN")}";

await botService.SetWebhookAsync(webhookUrl);

BotLogger.Info($"Bot started on port {port}");

// =============== RUN =============================
app.Run();
