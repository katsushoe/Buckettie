using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Buckettie.ApprovalPrompt;

internal sealed class PromptExceptionReporter(
    Func<Exception, string> writeDump,
    Action<string> showDialog,
    Func<bool> isUserInteractive)
{
    private int _reported;

    internal void Report(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (Interlocked.Exchange(ref _reported, 1) != 0)
        {
            return;
        }

        string? logPath = null;
        try
        {
            logPath = writeDump(exception);
        }
        catch (Exception)
        {
            // Reporting must not replace the original failure.
        }

        bool interactive;
        try
        {
            interactive = isUserInteractive();
        }
        catch (Exception)
        {
            interactive = false;
        }

        if (!interactive)
        {
            return;
        }

        string message = CreateMessage(logPath, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        try
        {
            showDialog(message);
        }
        catch (Exception)
        {
            // A failed desktop notification must not cause a second unhandled exception.
        }
    }

    internal static string CreateMessage(string? logPath, string language)
    {
        bool japanese = string.Equals(language, "ja", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            return japanese
                ? $"予期しないエラーが発生しました。処理は安全のため中止しました。詳細ログ: {logPath}"
                : $"An unexpected error occurred. The operation was stopped safely. Details: {logPath}";
        }

        return japanese
            ? "予期しないエラーが発生しました。処理は安全のため中止しました。詳細ログを保存できませんでした。"
            : "An unexpected error occurred. The operation was stopped safely. The diagnostic log could not be saved.";
    }
}

internal static partial class PromptExceptionDumpWriter
{
    private static readonly object FileLock = new();

    internal static string Write(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return Write(exception, Path.Combine(localAppData, "Akatsukisoft", "Buckettie", "logs"));
    }

    internal static string Write(Exception exception, string directory)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        DateTimeOffset occurredAt = DateTimeOffset.Now;
        string path = Path.Combine(directory, $"approval-prompt-{occurredAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
        string dump = Redact(exception.ToString());
        string occurredAtText = occurredAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
        string entry = $"===== {occurredAtText} ====={Environment.NewLine}{dump}{Environment.NewLine}{Environment.NewLine}";
        lock (FileLock)
        {
            File.AppendAllText(path, entry, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return path;
    }

    internal static string Redact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string redacted = BearerTokenRegex().Replace(value, "Bearer (redacted)");
        redacted = SecretAssignmentRegex().Replace(redacted, "$1(redacted)");
        return UrlCredentialRegex().Replace(redacted, "$1(redacted)@");
    }

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?i)([""']?\b(?:access[_-]?token|refresh[_-]?token|client[_-]?secret|api[_-]?(?:token|key)|token|password|secret|authorization|credential|private[_-]?key)\b[""']?\s*[:=]\s*)(?:""[^""]*""|'[^']*'|[^\s,;}]+)")]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"(?i)(https?://)[^/@\s]+@")]
    private static partial Regex UrlCredentialRegex();
}

internal static class PromptStaThread
{
    internal static Task<TResult> RunAsync<TResult>(Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        TaskCompletionSource<TResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Buckettie Approval Prompt UI",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}

internal static class PromptExecutionBoundary
{
    internal const int UnhandledExceptionExitCode = 4;

    internal static async Task<int> RunAsync(Func<Task<int>> action, Action<Exception> report)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(report);
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            report(exception);
            return UnhandledExceptionExitCode;
        }
    }
}

internal static class PromptDesktopGuard
{
    internal static Task<TResult> RunAsync<TResult>(
        bool userInteractive,
        Func<Task<TResult>> showDialog,
        TResult fallback)
    {
        ArgumentNullException.ThrowIfNull(showDialog);
        return userInteractive ? showDialog() : Task.FromResult(fallback);
    }
}
