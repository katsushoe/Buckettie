namespace Buckettie.Infrastructure.Credentials;

internal interface IWindowsCredentialApi
{
    internal CredentialApiResult Write(string targetName, string userName, byte[] secret);

    internal CredentialApiResult Read(string targetName);

    internal CredentialApiResult Delete(string targetName);
}

internal sealed record CredentialApiResult(bool IsSuccess, byte[]? Secret, int ErrorCode)
{
    internal static CredentialApiResult Success() => new(true, null, 0);

    internal static CredentialApiResult Success(byte[] secret) => new(true, secret, 0);

    internal static CredentialApiResult Failure(int errorCode) => new(false, null, errorCode);
}
