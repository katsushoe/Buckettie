using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
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
/// <c>CreateProcessWithTokenW</c>で対話Desktop(winsta0\default)へprocessを起動します。
/// LocalSystemは通常SeImpersonatePrivilegeを保有するため、AdjustTokenPrivilegesは不要です。
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class TokenProcessLauncher : IApprovalProcessLauncher
{
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNoWindow = 0x08000000;
    private const uint LogonWithProfile = 0x00000001;

    /// <inheritdoc />
    public bool TryLaunch(SafeAccessTokenHandle userToken, string executablePath, string argument)
    {
        ArgumentNullException.ThrowIfNull(userToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);

        StartupInfo startupInfo = new()
        {
            cb = Marshal.SizeOf<StartupInfo>(),
            lpDesktop = "winsta0\\default",
        };

        StringBuilder commandLine = new($"\"{executablePath}\" \"{argument}\"");

        IntPtr environmentBlock = IntPtr.Zero;
        try
        {
            // CreateProcessWithTokenWにNULL環境を渡すとSYSTEMの環境が引き継がれ、
            // SystemRoot等ToUserProfile由来の変数が欠落して.NET Hostが0xc0000142で
            // 初期化に失敗する。対象UserのProfileから環境Blockを明示的に構築する。
            if (!NativeMethods.CreateEnvironmentBlock(out environmentBlock, userToken, false))
            {
                return false;
            }

            bool created = NativeMethods.CreateProcessWithTokenW(
                userToken,
                LogonWithProfile,
                null,
                commandLine,
                CreateUnicodeEnvironment | CreateNoWindow,
                environmentBlock,
                null,
                ref startupInfo,
                out ProcessInformation processInformation);

            if (!created)
            {
                return false;
            }

            NativeMethods.CloseHandle(processInformation.hProcess);
            NativeMethods.CloseHandle(processInformation.hThread);
            return true;
        }
        finally
        {
            if (environmentBlock != IntPtr.Zero)
            {
                NativeMethods.DestroyEnvironmentBlock(environmentBlock);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    private static class NativeMethods
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcessWithTokenW(
            SafeAccessTokenHandle token,
            uint logonFlags,
            string? applicationName,
            StringBuilder commandLine,
            uint creationFlags,
            IntPtr environment,
            string? currentDirectory,
            ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateEnvironmentBlock(
            out IntPtr lpEnvironment,
            SafeAccessTokenHandle hToken,
            [MarshalAs(UnmanagedType.Bool)] bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);
    }
}
