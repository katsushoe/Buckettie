using System.ComponentModel;
using System.Diagnostics;

namespace Buckettie.Infrastructure.Git;

internal sealed class ProcessExecutor : IProcessExecutor
{
    public async Task<ProcessExecutionResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProcessStartInfo startInfo = CreateStartInfo(request);
        using Process process = new() { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return new(null, string.Empty, string.Empty, true, false, false);
            }
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return new(null, string.Empty, string.Empty, true, false, false);
        }
        catch (Win32Exception exception)
        {
            return new(null, string.Empty, exception.Message, false, false, false);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        if (request.StandardInput is not null)
        {
            await process.StandardInput.WriteAsync(request.StandardInput).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        using CancellationTokenSource timeout = new(request.Timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            return new(
                process.ExitCode,
                await outputTask.ConfigureAwait(false),
                await errorTask.ConfigureAwait(false),
                false,
                !cancellationToken.IsCancellationRequested,
                cancellationToken.IsCancellationRequested);
        }

        return new(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false),
            false,
            false,
            false);
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.StandardInput is not null,
            CreateNoWindow = true,
        };
        GitEnvironmentSanitizer.Sanitize(startInfo.Environment);
        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string name, string value) in request.Environment)
        {
            startInfo.Environment[name] = value;
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["LC_ALL"] = "C";
        return startInfo;
    }
}
