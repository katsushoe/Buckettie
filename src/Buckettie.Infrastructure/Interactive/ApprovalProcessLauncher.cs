using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Buckettie.Infrastructure.Interactive;

/// <summary>
/// 対話コンソールSessionのUserTokenで承認Dialogのprocessを起動します。
/// </summary>
internal interface IApprovalProcessLauncher
{
    /// <summary>
    /// 指定したUserTokenでexecutableを起動します。起動できた場合のみtrueを返します。
    /// </summary>
    bool TryLaunch(SafeAccessTokenHandle userToken, string executablePath, string argument);
}

/// <summary>
/// Task SchedulerのInteractive Token機構(<c>/IT</c>)で対話Desktopへprocessを起動します。
/// <c>CreateProcessWithTokenW</c>によるToken偽装型のSession越境は、EDR/サンドボックス製品が
/// 典型的な侵害後の横移動手法として対話Desktopへの接続を拒否することがあるため採用しません。
/// Task Scheduler自体のSession越境実装を経由することで同じ拒否を回避します。
/// LocalSystemは全リポジトリ操作より広い権限を持つため、対象UserよりTaskの作成・実行・削除
/// 権限が不足することはありません。
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class TaskSchedulerProcessLauncher : IApprovalProcessLauncher
{
    private static readonly TimeSpan SchtasksTimeout = TimeSpan.FromSeconds(15);

    /// <inheritdoc />
    public bool TryLaunch(SafeAccessTokenHandle userToken, string executablePath, string argument)
    {
        ArgumentNullException.ThrowIfNull(userToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);

        string? accountName = TryGetAccountName(userToken);
        if (accountName is null)
        {
            return false;
        }

        string taskName = $"Buckettie-Approval-{Guid.NewGuid():N}";
        string commandLine = $"\"{executablePath}\" \"{argument}\"";
        try
        {
            if (!RunSchtasks(
                "/Create", "/TN", taskName,
                "/TR", commandLine,
                "/SC", "ONCE", "/ST", "23:59",
                "/RU", accountName, "/IT",
                "/RL", "LIMITED", "/F"))
            {
                return false;
            }

            return RunSchtasks("/Run", "/TN", taskName);
        }
        finally
        {
            RunSchtasks("/Delete", "/TN", taskName, "/F");
        }
    }

    private static string? TryGetAccountName(SafeAccessTokenHandle userToken)
    {
        try
        {
            using WindowsIdentity identity = new(userToken.DangerousGetHandle());
            return string.IsNullOrWhiteSpace(identity.Name) ? null : identity.Name;
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool RunSchtasks(params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "schtasks.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
        {
            return false;
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit((int)SchtasksTimeout.TotalMilliseconds);
        Task.WaitAll(outputTask, errorTask);
        return exited && process.ExitCode == 0;
    }
}
