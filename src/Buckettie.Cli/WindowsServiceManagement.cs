using System.Diagnostics;

namespace Buckettie.Cli;

internal sealed record ServiceCommandResult(int ExitCode, string StandardOutput);

internal interface IServiceCommandExecutor
{
    Task<ServiceCommandResult> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

internal sealed class ScServiceCommandExecutor : IServiceCommandExecutor
{
    public async Task<ServiceCommandResult> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(Path.Combine(Environment.SystemDirectory, "sc.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        try
        {
            using Process process = Process.Start(startInfo)!;
            string standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new(process.ExitCode, standardOutput);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new(1, string.Empty);
        }
    }
}

internal sealed class WindowsServiceManager(IServiceCommandExecutor executor, string binaryDirectory)
{
    internal const string ServiceName = "Buckettie";

    public async Task<int> ExecuteAsync(string[] command, TextWriter output, CancellationToken cancellationToken)
    {
        return command switch
        {
            ["service", "install"] => await InstallAsync(output, cancellationToken).ConfigureAwait(false),
            ["service", "uninstall"] => await RunAsync(["delete", ServiceName], "Service uninstalled", output, cancellationToken).ConfigureAwait(false),
            ["service", "status"] or ["status"] => await StatusAsync(output, cancellationToken).ConfigureAwait(false),
            ["start"] => await RunAsync(["start", ServiceName], "Service started", output, cancellationToken).ConfigureAwait(false),
            ["stop"] => await RunAsync(["stop", ServiceName], "Service stopped", output, cancellationToken).ConfigureAwait(false),
            ["restart"] => await RestartAsync(output, cancellationToken).ConfigureAwait(false),
            _ => -1,
        };
    }

    private async Task<int> InstallAsync(TextWriter output, CancellationToken cancellationToken)
    {
        string server = Path.GetFullPath(Path.Combine(binaryDirectory, "Buckettie.Server.exe"));
        string configuration = Path.GetFullPath(Path.Combine(binaryDirectory, "..", "config", "buckettie.json"));
        if (!File.Exists(server) || !File.Exists(configuration))
        {
            output.WriteLine("[NG] Service install (RequiredFileMissing)");
            return 1;
        }

        string imagePath = $"\"{server}\" \"{configuration}\"";
        return await RunAsync(
            ["create", ServiceName, "binPath=", imagePath, "start=", "auto", "DisplayName=", "Buckettie MCP Server"],
            "Service installed; configure Log On account before start",
            output,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> RestartAsync(TextWriter output, CancellationToken cancellationToken)
    {
        ServiceCommandResult query = await executor.ExecuteAsync(["query", ServiceName], cancellationToken).ConfigureAwait(false);
        if (query.ExitCode != 0)
        {
            output.WriteLine("[NG] Service restart");
            return 1;
        }
        if (query.StandardOutput.Contains("RUNNING", StringComparison.Ordinal))
        {
            ServiceCommandResult stop = await executor.ExecuteAsync(["stop", ServiceName], cancellationToken).ConfigureAwait(false);
            if (stop.ExitCode != 0)
            {
                output.WriteLine("[NG] Service restart");
                return 1;
            }
        }
        return await RunAsync(["start", ServiceName], "Service restarted", output, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> StatusAsync(TextWriter output, CancellationToken cancellationToken)
    {
        ServiceCommandResult result = await executor.ExecuteAsync(["query", ServiceName], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            output.WriteLine("[NG] Service: NotInstalled");
            return 1;
        }
        string state = result.StandardOutput.Contains("RUNNING", StringComparison.Ordinal) ? "Running"
            : result.StandardOutput.Contains("STOPPED", StringComparison.Ordinal) ? "Stopped" : "Pending";
        output.WriteLine($"[OK] Service: {state}");
        return 0;
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, string successMessage,
        TextWriter output, CancellationToken cancellationToken)
    {
        ServiceCommandResult result = await executor.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
        output.WriteLine($"[{(result.ExitCode == 0 ? "OK" : "NG")}] {(result.ExitCode == 0 ? successMessage : successMessage.Split(' ')[0])}");
        return result.ExitCode == 0 ? 0 : 1;
    }
}
