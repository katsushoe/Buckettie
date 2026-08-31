using System.IO.Pipes;
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
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 5 && string.Equals(args[0], "--token", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(args[1])
            && !string.IsNullOrWhiteSpace(args[2])
            && !string.IsNullOrWhiteSpace(args[3])
            && !string.IsNullOrWhiteSpace(args[4]))
        {
            return RunTokenPromptAsync(args[1], args[2], args[3], args[4]).GetAwaiter().GetResult();
        }

        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            return InvalidArgsExitCode;
        }

        return RunAsync(args[0]).GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(string pipeName)
    {
        await using NamedPipeClientStream pipe = new(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using CancellationTokenSource connectTimeout = new(ConnectTimeout);
        try
        {
            await pipe.ConnectAsync(connectTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
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

        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

        bool approved;
        string? token;
        using (ApprovalForm form = new(request))
        {
            approved = form.ShowDialog() == DialogResult.Yes;
            token = approved ? form.Token : null;
        }

        try
        {
            await ApprovalPipeProtocol
                .WriteFrameAsync(pipe, new ApprovalPromptResponse(approved, token), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (IOException)
        {
            return PipeUnavailableExitCode;
        }

        return 0;
    }

    private static async Task<int> RunTokenPromptAsync(
        string pipeName,
        string repository,
        string remoteUrl,
        string language)
    {
        await using NamedPipeClientStream pipe = new(
            ".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        using CancellationTokenSource connectTimeout = new(ConnectTimeout);
        try
        {
            await pipe.ConnectAsync(connectTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            return PipeUnavailableExitCode;
        }

        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

        string? token;
        using (TokenForm form = new(repository, remoteUrl, language))
        {
            token = form.ShowDialog() == DialogResult.OK ? form.Token : null;
        }

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

        return 0;
    }
}
