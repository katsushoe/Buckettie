using System.Security.Cryptography;
using System.Text;
using Buckettie.Application.Credentials;
using Buckettie.Application.Repositories;

namespace Buckettie.Infrastructure.Credentials;

/// <summary>
/// Windows Credential ManagerへBitbucket API Tokenを保存します。
/// </summary>
public sealed class WindowsCredentialManagerTokenStore : IApiTokenStore
{
    private const int MaximumCredentialBytes = 2560;
    private const int ErrorNotFound = 1168;
    private const int ErrorPlatformNotSupported = -1;
    private const string TargetPrefix = "Buckettie/Bitbucket/";
    private readonly IWindowsCredentialApi _api;

    /// <summary>
    /// Windows Credential Managerを使用するStoreを初期化します。
    /// </summary>
    public WindowsCredentialManagerTokenStore()
        : this(new WindowsCredentialApi())
    {
    }

    internal WindowsCredentialManagerTokenStore(IWindowsCredentialApi api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <inheritdoc />
    public ApiTokenStoreResult Save(string repositoryId, string token)
    {
        ApiTokenStoreResult? validation = ValidateRepositoryId(repositoryId);
        if (validation is not null)
        {
            return validation;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiTokenStoreResult.Failure(ApiTokenStoreError.InvalidToken);
        }

        byte[] secret = Encoding.UTF8.GetBytes(token);
        try
        {
            if (secret.Length > MaximumCredentialBytes)
            {
                return ApiTokenStoreResult.Failure(ApiTokenStoreError.TokenTooLong);
            }

            CredentialApiResult result = _api.Write(CreateTarget(repositoryId), repositoryId, secret);
            return MapProviderResult(result);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <inheritdoc />
    public ApiTokenStoreResult Read(string repositoryId)
    {
        ApiTokenStoreResult? validation = ValidateRepositoryId(repositoryId);
        if (validation is not null)
        {
            return validation;
        }

        CredentialApiResult result = _api.Read(CreateTarget(repositoryId));
        if (!result.IsSuccess)
        {
            return MapProviderResult(result);
        }

        if (result.Secret is null || result.Secret.Length == 0)
        {
            return ApiTokenStoreResult.Failure(ApiTokenStoreError.InvalidToken);
        }

        try
        {
            return ApiTokenStoreResult.Success(Encoding.UTF8.GetString(result.Secret));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(result.Secret);
        }
    }

    /// <inheritdoc />
    public ApiTokenStoreResult Delete(string repositoryId)
    {
        ApiTokenStoreResult? validation = ValidateRepositoryId(repositoryId);
        if (validation is not null)
        {
            return validation;
        }

        CredentialApiResult result = _api.Delete(CreateTarget(repositoryId));
        return !result.IsSuccess && result.ErrorCode == ErrorNotFound
            ? ApiTokenStoreResult.Success()
            : MapProviderResult(result);
    }

    private static ApiTokenStoreResult? ValidateRepositoryId(string repositoryId)
    {
        if (!RepositoryId.IsValid(repositoryId))
        {
            return ApiTokenStoreResult.Failure(ApiTokenStoreError.InvalidRepositoryId);
        }

        return null;
    }

    private static string CreateTarget(string repositoryId) => $"{TargetPrefix}{repositoryId}";

    private static ApiTokenStoreResult MapProviderResult(CredentialApiResult result)
    {
        if (result.IsSuccess)
        {
            return ApiTokenStoreResult.Success();
        }

        ApiTokenStoreError error = result.ErrorCode switch
        {
            ErrorNotFound => ApiTokenStoreError.TokenNotFound,
            ErrorPlatformNotSupported => ApiTokenStoreError.PlatformNotSupported,
            _ => ApiTokenStoreError.ProviderFailure,
        };
        return ApiTokenStoreResult.Failure(error, result.ErrorCode);
    }
}
