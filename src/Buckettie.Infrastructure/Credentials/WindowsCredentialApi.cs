using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Buckettie.Infrastructure.Credentials;

internal sealed class WindowsCredentialApi : IWindowsCredentialApi
{
    private const uint GenericCredential = 1;
    private const uint LocalMachinePersistence = 2;

    public CredentialApiResult Write(string targetName, string userName, byte[] secret)
    {
        if (!OperatingSystem.IsWindows())
        {
            return CredentialApiResult.Failure((int)NativeError.PlatformNotSupported);
        }

        IntPtr secretPointer = Marshal.AllocCoTaskMem(secret.Length);
        try
        {
            Marshal.Copy(secret, 0, secretPointer, secret.Length);
            NativeCredential credential = new()
            {
                Type = GenericCredential,
                TargetName = targetName,
                CredentialBlobSize = (uint)secret.Length,
                CredentialBlob = secretPointer,
                Persist = LocalMachinePersistence,
                UserName = userName,
            };

            return CredWrite(ref credential, 0)
                ? CredentialApiResult.Success()
                : CredentialApiResult.Failure(Marshal.GetLastPInvokeError());
        }
        finally
        {
            byte[] cleared = new byte[secret.Length];
            Marshal.Copy(cleared, 0, secretPointer, cleared.Length);
            CryptographicOperations.ZeroMemory(cleared);
            Marshal.FreeCoTaskMem(secretPointer);
        }
    }

    public CredentialApiResult Read(string targetName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return CredentialApiResult.Failure((int)NativeError.PlatformNotSupported);
        }

        if (!CredRead(targetName, GenericCredential, 0, out IntPtr credentialPointer))
        {
            return CredentialApiResult.Failure(Marshal.GetLastPInvokeError());
        }

        try
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            byte[] secret = new byte[credential.CredentialBlobSize];
            if (secret.Length > 0)
            {
                Marshal.Copy(credential.CredentialBlob, secret, 0, secret.Length);
            }

            return CredentialApiResult.Success(secret);
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public CredentialApiResult Delete(string targetName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return CredentialApiResult.Failure((int)NativeError.PlatformNotSupported);
        }

        return CredDelete(targetName, GenericCredential, 0)
            ? CredentialApiResult.Success()
            : CredentialApiResult.Failure(Marshal.GetLastPInvokeError());
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint reservedFlag,
        out IntPtr credentialPointer);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        internal uint Flags;
        internal uint Type;
        internal string? TargetName;
        internal string? Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint CredentialBlobSize;
        internal IntPtr CredentialBlob;
        internal uint Persist;
        internal uint AttributeCount;
        internal IntPtr Attributes;
        internal string? TargetAlias;
        internal string? UserName;
    }

    private enum NativeError
    {
        PlatformNotSupported = -1,
    }
}
