using System.Diagnostics;
using System.IO.Pipes;
using Buckettie.Application.Interactive;

namespace Buckettie.Cli;

/// <summary>Token入力Dialogを起動し、使い捨てNamed Pipeから結果を受け取ります。</summary>
internal static class TokenPromptClient
{
    private static readonly TimeSpan PromptTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Token入力Dialogを表示します。Cancelまたは起動失敗ではnullを返します。</summary>
    internal static async Task<string?> ReadTokenAsync(
        string repository,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        string pipeName = $"Buckettie-Token-{Guid.NewGuid():N}";
        await using NamedPipeServerStream pipe = new(
            pipeName,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        string executable = Path.Combine(AppContext.BaseDirectory, "Buckettie.ApprovalPrompt.exe");
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--token");
        startInfo.ArgumentList.Add(pipeName);
        startInfo.ArgumentList.Add(repository);
        startInfo.ArgumentList.Add(language);

        try
        {
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException();
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PromptTimeout);
            await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
            TokenPromptResponse? response = await ApprovalPipeProtocol
                .ReadFrameAsync<TokenPromptResponse>(pipe, timeout.Token).ConfigureAwait(false);
            return response?.Token;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception
            or OperationCanceledException)
        {
            return null;
        }
    }
}
