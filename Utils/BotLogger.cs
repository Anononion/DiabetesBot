using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace DiabetesBot.Utils;

public static class BotLogger
{
    // Уровни
    public enum Level { TRACE = 0, DEBUG = 1, INFO = 2, WARN = 3, ERROR = 4, FATAL = 5 }

    // Настройки
    private static readonly string LogDir = Environment.GetEnvironmentVariable("LOG_PATH") ?? Path.Combine("Data", "logs");
    private static readonly bool ToFile = (Environment.GetEnvironmentVariable("LOG_TO_FILE") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
    private static readonly bool ToConsole = (Environment.GetEnvironmentVariable("LOG_TO_CONSOLE") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
    private static readonly Level MinLevel = Enum.TryParse<Level>(Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "INFO", true, out var lvl) ? lvl : Level.INFO;

    private const long MaxFileBytes = 10L * 1024 * 1024; // 10 MB
    private static readonly object FileLock = new();

    // Контекст логов (AsyncLocal)
    private static readonly AsyncLocal<LogContext?> CurrentCtx = new();

    // Очередь для записи (чтобы не блокировать)
    private static readonly BlockingCollection<string> WriteQueue = new(new ConcurrentQueue<string>());
    private static readonly Thread WriterThread;

    // JSON options
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static Logger()
    {
        Directory.CreateDirectory(LogDir);

        // фоновая нить записи
        WriterThread = new Thread(WriterLoop) { IsBackground = true, Name = "LoggerWriter" };
        WriterThread.Start();
    }

    // Публичные методы
    public static IDisposable Scope(string operation, long? userId = null, long? chatId = null, string? correlationId = null)
    {
        var prev = CurrentCtx.Value;
        var scope = new LogScope(prev, operation, userId, chatId, correlationId);
        CurrentCtx.Value = scope.Context;
        return scope;
    }

    public static void Trace(string msg, object? data = null) => Write(Level.TRACE, msg, data);
    public static void Debug(string msg, object? data = null) => Write(Level.DEBUG, msg, data);
    public static void Info(string msg, object? data = null) => Write(Level.INFO, msg, data);
    public static void Warn(string msg, object? data = null) => Write(Level.WARN, msg, data);
    public static void Error(string msg, Exception? ex = null, object? data = null) =>
        Write(Level.ERROR, msg, Merge(data, new { exception = ex?.ToString() }));
    public static void Fatal(string msg, Exception? ex = null, object? data = null) =>
        Write(Level.FATAL, msg, Merge(data, new { exception = ex?.ToString() }));

    // Внутреннее
    private static void Write(Level level, string message, object? data)
    {
        if (level < MinLevel) return;

        var ctx = CurrentCtx.Value ?? LogContext.Empty;
        var payload = new
        {
            ts = DateTimeOffset.UtcNow.ToString("O"),
            level = level.ToString(),
            msg = message,
            op = ctx.Operation,
            dur_ms = ctx.ElapsedMs(),
            userId = ctx.UserId,
            chatId = ctx.ChatId,
            corr = ctx.CorrelationId,
            data
        };

        var line = JsonSerializer.Serialize(payload, JsonOpts);

        if (ToConsole)
            Console.WriteLine(line);

        if (ToFile)
            WriteQueue.Add(line);
    }

    private static void WriterLoop()
    {
        string currentPath = GetLogPath();
        using var file = new StreamWriter(new FileStream(currentPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        { AutoFlush = true };

        foreach (var line in WriteQueue.GetConsumingEnumerable())
        {
            lock (FileLock)
            {
                // Daily/size rotation
                var target = GetLogPath();
                if (!string.Equals(target, currentPath, StringComparison.OrdinalIgnoreCase) ||
                    new FileInfo(currentPath).Length > MaxFileBytes)
                {
                    file.Flush();
                    try { (file.BaseStream as FileStream)?.Dispose(); } catch { }

                    currentPath = target;
                }
            }
        }
    }

    private static string GetLogPath()
    {
        var fname = $"bot_{DateTime.UtcNow:yyyy-MM-dd}.log";
        return Path.Combine(LogDir, fname);
    }

    private static object? Merge(object? a, object? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        // грубо: объединяем как строку
        return new { a, b };
    }

    // Модели контекста/скоупа
    private sealed class LogContext
    {
        public static readonly LogContext Empty = new(null, null, null, null);

        public string? Operation { get; }
        public long? UserId { get; }
        public long? ChatId { get; }
        public string CorrelationId { get; }
        private readonly Stopwatch _sw;

        public LogContext(string? operation, long? userId, long? chatId, string? correlationId)
        {
            Operation = operation;
            UserId = userId;
            ChatId = chatId;
            CorrelationId = string.IsNullOrEmpty(correlationId) ? Guid.NewGuid().ToString("N") : correlationId!;
            _sw = Stopwatch.StartNew();
        }

        public long ElapsedMs() => (long)_sw.Elapsed.TotalMilliseconds;
    }

    private sealed class LogScope : IDisposable
    {
        private readonly LogContext? _prev;
        public LogContext Context { get; }
        private bool _disposed;

        public LogScope(LogContext? prev, string operation, long? userId, long? chatId, string? correlationId)
        {
            _prev = prev;
            Context = new LogContext(operation, userId, chatId, correlationId);
            // стартовая запись
            Write(Level.TRACE, $"→ {operation} start", null);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Write(Level.TRACE, $"← {Context.Operation} end", new { elapsed_ms = Context.ElapsedMs() });
            CurrentCtx.Value = _prev;
        }
    }
}
