using System.Diagnostics;
using System.IO.Pipes;
using Buckettie.Application.Interactive;

namespace Buckettie.Cli;

/// <summary>Token入力Dialogを起動し、使い捨てNamed Pipeから結果を受け取ります。</summary>
internal static class TokenPromptClient
{
    /// <summary>Token入力Dialogを表示します。Cancelではnull、障害では識別可能な例外を返します。</summary>
    /// <remarks>入力に期限は設けません。利用者が応答するかDialog processが終了するまで待ちます。</remarks>
    internal static Task<string?> ReadTokenAsync(
        string repository,
        string remoteUrl,
        string language,
        CancellationToken cancellationToken)
        => ReadTokenCoreAsync(repository, remoteUrl, language, cancellationToken, Process.Start);

    internal static async Task<string?> ReadTokenCoreAsync(
        string repository, string remoteUrl, string language, CancellationToken cancellationToken,
        Func<ProcessStartInfo, Process?> startProcess)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);
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
        startInfo.ArgumentList.Add(remoteUrl);
        startInfo.ArgumentList.Add(language);

        try
        {
            using Process process = startProcess(startInfo) ?? throw new InvalidOperationException();
            // A dialog that exits without connecting would otherwise leave the CLI waiting forever.
            Task connection = pipe.WaitForConnectionAsync(cancellationToken);
            Task exited = process.WaitForExitAsync(cancellationToken);
            if (await Task.WhenAny(connection, exited).ConfigureAwait(false) != connection)
            {
                _ = connection.ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException();
            }
            await connection.ConfigureAwait(false);
            // The dialog closes the pipe when it exits, so this read ends without a deadline.
            TokenPromptResponse? response = await ApprovalPipeProtocol
                .ReadFrameAsync<TokenPromptResponse>(pipe, cancellationToken).ConfigureAwait(false);
            return response?.Token;
        }
        catch (OperationCanceledException)
        {
            throw new TokenPromptException(TokenPromptError.Cancelled);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            throw new TokenPromptException(TokenPromptError.LaunchFailed);
        }
    }
}

internal enum TokenPromptError { Cancelled, LaunchFailed }

internal sealed class TokenPromptException(TokenPromptError error)
    : Exception($"TokenPrompt{error}");
