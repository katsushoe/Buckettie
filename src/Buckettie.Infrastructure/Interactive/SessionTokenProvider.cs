using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Buckettie.Infrastructure.Interactive;

/// <summary>
/// 現在の対話コンソールSessionにログオン中のUserTokenとSIDを取得します。
/// </summary>
internal interface ISessionTokenProvider
{
    /// <summary>
    /// 対話コンソールSessionが存在すれば、そのUserTokenとSIDを返します。
    /// </summary>
    bool TryGetActiveSessionToken(out SafeAccessTokenHandle? token, out SecurityIdentifier? userSid);
}

/// <summary>WTS APIで対話コンソールSessionのUserTokenとSIDを取得します。</summary>
[SupportedOSPlatform("windows")]
internal sealed class WtsSessionTokenProvider : ISessionTokenProvider
{
    private const uint InvalidSessionId = 0xFFFFFFFF;

    /// <inheritdoc />
    public bool TryGetActiveSessionToken(out SafeAccessTokenHandle? token, out SecurityIdentifier? userSid)
    {
        token = null;
        userSid = null;
        uint sessionId = NativeMethods.WTSGetActiveConsoleSessionId();
        if (sessionId == InvalidSessionId)
        {
            return false;
        }

        if (!NativeMethods.WTSQueryUserToken(sessionId, out SafeAccessTokenHandle handle) || handle.IsInvalid)
        {
            return false;
        }

        try
        {
            using WindowsIdentity identity = new(handle.DangerousGetHandle());
            if (identity.User is null)
            {
                handle.Dispose();
                return false;
            }

            token = handle;
            userSid = identity.User;
            return true;
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException)
        {
            handle.Dispose();
            return false;
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        internal static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WTSQueryUserToken(uint sessionId, out SafeAccessTokenHandle token);
    }
}
