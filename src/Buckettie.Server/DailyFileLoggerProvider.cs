using Microsoft.Extensions.Logging;

namespace Buckettie.Server;

internal sealed class DailyFileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly Lock _lock = new();

    public ILogger CreateLogger(string categoryName) => new DailyFileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(LogLevel level, string category, string message)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"buckettie-{now:yyyyMMdd}.log");
        char levelCode = level switch
        {
            LogLevel.Trace => 'T',
            LogLevel.Debug => 'D',
            LogLevel.Information => 'I',
            LogLevel.Warning => 'W',
            LogLevel.Error => 'E',
            LogLevel.Critical => 'C',
            _ => 'N',
        };
        string line = $"{now:yyyy-MM-dd'T'HH:mm:ss.fffzzz} [{levelCode}] [{category}] {message}{Environment.NewLine}";
        lock (_lock)
        {
            File.AppendAllText(path, line);
        }
    }

    private sealed class DailyFileLogger(DailyFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception));
            }
        }
    }
}
