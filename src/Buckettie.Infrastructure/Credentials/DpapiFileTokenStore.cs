using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;
using Buckettie.Application.Credentials;
using Buckettie.Application.Repositories;

namespace Buckettie.Infrastructure.Credentials;

/// <summary>DPAPI LocalMachineで暗号化したRepository別Tokenを保存します。</summary>
public sealed class DpapiFileTokenStore : IApiTokenStore
{
    private const int MaximumTokenBytes = 2560;
    private static readonly byte[] Entropy = "Buckettie/Bitbucket/DPAPI/v1"u8.ToArray();
    private readonly string _directory;
    private readonly IDpapiProtector _protector;
    private readonly ISecretDirectorySecurity _security;

    /// <summary>暗号化Token Storeを初期化します。</summary>
    public DpapiFileTokenStore(string directory)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _protector = new WindowsDpapiProtector();
        _security = new WindowsSecretDirectorySecurity();
    }

    internal DpapiFileTokenStore(string directory, IDpapiProtector protector, ISecretDirectorySecurity security)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _protector = protector;
        _security = security;
    }

    /// <inheritdoc />
    public ApiTokenStoreResult Save(string repositoryId, string token)
    {
        ApiTokenStoreResult? validation = Validate(repositoryId);
        if (validation is not null) return validation;
        if (string.IsNullOrWhiteSpace(token)) return ApiTokenStoreResult.Failure(ApiTokenStoreError.InvalidToken);
        byte[] clearText = Encoding.UTF8.GetBytes(token);
        if (clearText.Length > MaximumTokenBytes)
        {
            CryptographicOperations.ZeroMemory(clearText);
            return ApiTokenStoreResult.Failure(ApiTokenStoreError.TokenTooLong);
        }
        byte[]? protectedData = null;
        try
        {
            _security.Ensure(_directory);
            protectedData = _protector.Protect(clearText, Entropy);
            string target = GetPath(repositoryId);
            string temporary = $"{target}.{Guid.NewGuid():N}.tmp";
            File.WriteAllBytes(temporary, protectedData);
            File.Move(temporary, target, true);
            return ApiTokenStoreResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or PlatformNotSupportedException)
        {
            return ApiTokenStoreResult.Failure(exception is PlatformNotSupportedException
                ? ApiTokenStoreError.PlatformNotSupported : ApiTokenStoreError.ProviderFailure);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearText);
            if (protectedData is not null) CryptographicOperations.ZeroMemory(protectedData);
        }
    }

    /// <inheritdoc />
    public ApiTokenStoreResult Read(string repositoryId)
    {
        ApiTokenStoreResult? validation = Validate(repositoryId);
        if (validation is not null) return validation;
        string path = GetPath(repositoryId);
        if (!File.Exists(path)) return ApiTokenStoreResult.Failure(ApiTokenStoreError.TokenNotFound);
        byte[]? protectedData = null;
        byte[]? clearText = null;
        try
        {
            protectedData = File.ReadAllBytes(path);
            clearText = _protector.Unprotect(protectedData, Entropy);
            return clearText.Length == 0
                ? ApiTokenStoreResult.Failure(ApiTokenStoreError.InvalidToken)
                : ApiTokenStoreResult.Success(Encoding.UTF8.GetString(clearText));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or PlatformNotSupportedException)
        {
            return ApiTokenStoreResult.Failure(exception is PlatformNotSupportedException
                ? ApiTokenStoreError.PlatformNotSupported : ApiTokenStoreError.ProviderFailure);
        }
        finally
        {
            if (protectedData is not null) CryptographicOperations.ZeroMemory(protectedData);
            if (clearText is not null) CryptographicOperations.ZeroMemory(clearText);
        }
    }

    /// <inheritdoc />
    public ApiTokenStoreResult Delete(string repositoryId)
    {
        ApiTokenStoreResult? validation = Validate(repositoryId);
        if (validation is not null) return validation;
        string path = GetPath(repositoryId);
        if (!File.Exists(path)) return ApiTokenStoreResult.Success();
        try
        {
            File.Delete(path);
            return ApiTokenStoreResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ApiTokenStoreResult.Failure(ApiTokenStoreError.ProviderFailure);
        }
    }

    private string GetPath(string repositoryId) => Path.Combine(_directory, $"{repositoryId}.token");
    private static ApiTokenStoreResult? Validate(string repositoryId) => RepositoryId.IsValid(repositoryId)
        ? null : ApiTokenStoreResult.Failure(ApiTokenStoreError.InvalidRepositoryId);
}

internal interface IDpapiProtector
{
    byte[] Protect(byte[] clearText, byte[] entropy);
    byte[] Unprotect(byte[] protectedData, byte[] entropy);
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsDpapiProtector : IDpapiProtector
{
    public byte[] Protect(byte[] clearText, byte[] entropy) =>
        ProtectedData.Protect(clearText, entropy, DataProtectionScope.LocalMachine);
    public byte[] Unprotect(byte[] protectedData, byte[] entropy) =>
        ProtectedData.Unprotect(protectedData, entropy, DataProtectionScope.LocalMachine);
}

internal interface ISecretDirectorySecurity
{
    void Ensure(string directory);
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsSecretDirectorySecurity : ISecretDirectorySecurity
{
    public void Ensure(string directory)
    {
        Directory.CreateDirectory(directory);
        DirectorySecurity security = new();
        security.SetAccessRuleProtection(true, false);
        FileSystemRights rights = FileSystemRights.FullControl;
        InheritanceFlags inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            rights, inheritance, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            rights, inheritance, PropagationFlags.None, AccessControlType.Allow));
        SecurityIdentifier? currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            security.AddAccessRule(new FileSystemAccessRule(currentUser, rights, inheritance,
                PropagationFlags.None, AccessControlType.Allow));
        }
        new DirectoryInfo(directory).SetAccessControl(security);
    }
}
