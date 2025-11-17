using DiabetesBot;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);

Logger.Info("Starting DiabetesBot...");

// Load env
var env = new EnvConfigService();
env.LoadAndDecryptEnv();

string? botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
if (string.IsNullOrEmpty(botToken))
{
    Logger.Error("BOT_TOKEN not found", new Exception("Missing token"));
    return;
}

var botClient = new TelegramBotClient(botToken);

builder.Services.AddSingleton<BotService>();
builder.Services.AddSingleton<TelegramBotClient>(botClient);

var app = builder.Build();

// -------- WEBHOOK ENDPOINT --------
app.MapPost("/bot/{token}", async (
    HttpRequest request,
    string token,
    BotService botService) =>
{
    // Reject wrong token
    if (token != botToken)
        return Results.Unauthorized();

    var update = await request.ReadFromJsonAsync<Update>();
    if (update != null)
        await botService.HandleUpdateAsync(update);

    return Results.Ok();
});

// -------- HEALTH CHECK --------
app.MapGet("/", () => "Bot is running");

app.Run();
