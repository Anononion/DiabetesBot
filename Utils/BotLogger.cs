namespace DiabetesBot.Utils;

public static class BotLogger
{
    public enum Level { TRACE = 0, DEBUG = 1, INFO = 2, WARN = 3, ERROR = 4, FATAL = 5 }

    private static readonly string LogDir = Environment.GetEnvironmentVariable("LOG_PATH") ?? Path.Combine("Data", "logs");
    private static readonly bool ToFile = (Environment.GetEnvironmentVariable("LOG_TO_FILE") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
    private static readonly bool ToConsole = (Environment.GetEnvironmentVariable("LOG_TO_CONSOLE") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
    private static readonly Level MinLevel = Enum.TryParse<Level>(Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "INFO", true, out var lvl) ? lvl : Level.INFO;

    private const long MaxFileBytes = 10L * 1024 * 1024;
    private static readonly object FileLock = new();

    private static readonly AsyncLocal<LogContext?> CurrentCtx = new();
    private static readonly BlockingCollection<string> WriteQueue = new(new ConcurrentQueue<string>());
    private static readonly Thread WriterThread;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static BotLogger()
    {
        Directory.CreateDirectory(LogDir);
        WriterThread = new Thread(WriterLoop) { IsBackground = true, Name = "LoggerWriter" };
        WriterThread.Start();
    }

    public static IDisposable Scope(string operation, long? userId = null, long? chatId = null, string? correlationId = null)
    {
        var prev = CurrentCtx.Value;
        var scope = new LogScope(prev, operation, userId, chatId, correlationId);
        CurrentCtx.Value = scope.Context;
        return scope;
    }

    public static void Trace(string msg, object? data = null) => Write(Level.TRACE, msg, data);
    public static void Debug(string msg, object? data = null) => Write(Level.DEBUG, msg, data);
    public static void Info(string msg, object? data = null) => Wri
