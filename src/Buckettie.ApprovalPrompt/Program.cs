・ｿusing System.IO.Pipes;
using System.Windows.Forms;
using Buckettie.Application.Interactive;

namespace Buckettie.ApprovalPrompt;

/// <summary>
/// Buckettie Serverから起動される承認Dialog processです。
/// args[0]で渡された使い捨てNamed Pipeへ接続し、要求を表示して応答を書き戻します。
/// </summary>
internal static class Program
{
    private const int InvalidArgsExitCode = 2;
    private const int PipeUnavailableExitCode = 3;
    private const int NoInteractiveDesktopExitCode = 5;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    [STAThread]
    private static Task<int> Main(string[] args)
    {
        PromptExceptionReporter reporter = new(
            PromptExceptionDumpWriter.Write,
            ShowFatalErrorDialog,
            () => Environment.UserInteractive);
        return PromptExecutionBoundary.RunAsync(async () =>
        {
            RegisterGlobalExceptionHandlers(reporter);
            return await RunCommandAsync(args).ConfigureAwait(false);
        }, reporter.Report);
    }

    private static async Task<int> RunCommandAsync(string[] args)
    {
        if (args.Length == 5 && string.Equals(args[0], "--token", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(args[1])
            && !string.IsNullOrWhiteSpace(args[2])
            && !string.IsNullOrWhiteSpace(args[3])
            && !string.IsNullOrWhiteSpace(args[4]))
        {
            return await RunTokenPromptAsync(args[1], args[2], args[3], args[4]).ConfigureAwait(false);
        }

        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            return InvalidArgsExitCode;
        }

        return await RunAsync(args[0]).ConfigureAwait(false);
    }

    private static void RegisterGlobalExceptionHandlers(PromptExceptionReporter reporter)
    {
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, eventArgs) =>
        {
            reporter.Report(eventArgs.Exception);
            Environment.Exit(PromptExecutionBoundary.UnhandledExceptionExitCode);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Exception exception = eventArgs.ExceptionObject as Exception
                ?? new InvalidOperationException("An unknown unhandled exception occurred.");
            reporter.Report(exception);
            Environment.Exit(PromptExecutionBoundary.UnhandledExceptionExitCode);
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            eventArgs.SetObserved();
            reporter.Report(eventArgs.Exception);
            Environment.Exit(PromptExecutionBoundary.UnhandledExceptionExitCode);
        };
    }

    private static void ShowFatalErrorDialog(string message)
    {
        if (!Environment.UserInteractive)
        {
            return;
        }

        Thread dialogThread = new(() => System.Windows.Forms.MessageBox.Show(
            message,
            "Buckettie",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error))
        {
            IsBackground = true,
            Name = "Buckettie Approval Prompt Error Dialog",
        };
        dialogThread.SetApartmentState(ApartmentState.STA);
        dialogThread.Start();
        dialogThread.Join();
    }

    private static async Task<int> RunAsync(string pipeName)
    {
        await using NamedPipeClientStream pipe = new(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        if (!await ConnectAsync(pipe, ConnectTimeout).ConfigureAwait(false))
        {
            return PipeUnavailableExitCode;
        }

        ApprovalPromptRequest? request = await ApprovalPipeProtocol
            .ReadFrameAsync<ApprovalPromptRequest>(pipe, CancellationToken.None)
            .ConfigureAwait(false);
        if (request is null)
        {
            return InvalidArgsExitCode;
        }

        bool hasDesktop = Environment.UserInteractive;
        PromptDialogResult result = await PromptDesktopGuard.RunAsync(
            hasDesktop,
            () => PromptStaThread.RunAsync(() => ShowApprovalDialog(request)),
            PromptDialogResult.WithoutDesktop).ConfigureAwait(false);

        try
        {
            await ApprovalPipeProtocol
                .WriteFrameAsync(pipe, new ApprovalPromptResponse(result.Approved, result.Token), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (IOException)
        {
            return PipeUnavailableExitCode;
        }

        return hasDesktop ? 0 : NoInteractiveDesktopExitCode;
    }

    private static async Task<int> RunTokenPromptAsync(
        string pipeName,
        string repository,
        string remoteUrl,
        string language)
    {
        await using NamedPipeClientStream pipe = new(
            ".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        if (!await ConnectAsync(pipe, ConnectTimeout).ConfigureAwait(false))
        {
            return PipeUnavailableExitCode;
        }

        bool hasDesktop = Environment.UserInteractive;
        string? token = await PromptDesktopGuard.RunAsync(
            hasDesktop,
            () => PromptStaThread.RunAsync(() => ShowTokenDialog(repository, remoteUrl, language)),
            fallback: null).ConfigureAwait(false);

        try
        {
            await ApprovalPipeProtocol.WriteFrameAsync(
                pipe,
                new TokenPromptResponse(string.IsNullOrWhiteSpace(token) ? null : token),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return PipeUnavailableExitCode;
        }

        return hasDesktop ? 0 : NoInteractiveDesktopExitCode;
    }

    internal static async Task<bool> ConnectAsync(NamedPipeClientStream pipe, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        using CancellationTokenSource connectTimeout = new(timeout);
        try
        {
            await pipe.ConnectAsync(connectTimeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            return false;
        }
    }

    private static PromptDialogResult ShowApprovalDialog(ApprovalPromptRequest request)
    {
        InitializeWindowsForms();
        using ApprovalForm form = new(request);
        DialogResult dialogResult = form.ShowDialog();
        return PromptDialogResult.From(dialogResult, form.TimedOut, form.Token);
    }

    private static string? ShowTokenDialog(string repository, string remoteUrl, string language)
    {
        InitializeWindowsForms();
        using TokenForm form = new(repository, remoteUrl, language);
        return form.ShowDialog() == DialogResult.OK ? form.Token : null;
    }

    private static void InitializeWindowsForms()
    {
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
    }
}

internal sealed record PromptDialogResult(bool Approved, string? Token, bool TimedOut)
{
    internal static PromptDialogResult WithoutDesktop => new(false, null, false);

    internal static PromptDialogResult From(DialogResult result, bool timedOut, string? token)
    {
        bool approved = !timedOut && result == DialogResult.Yes;
        return new PromptDialogResult(approved, approved ? token : null, timedOut);
    }
}
