namespace Buckettie.Application.Credentials;

/// <summary>
/// API Token Storeのエラーコードです。
/// </summary>
public enum ApiTokenStoreError
{
    InvalidRepositoryId,
    InvalidToken,
    TokenTooLong,
    TokenNotFound,
    PlatformNotSupported,
    ProviderFailure,
}

/// <summary>
/// API Token Storeの操作結果を表します。
/// </summary>
public sealed record ApiTokenStoreResult(
    bool IsSuccess,
    string? Token,
    ApiTokenStoreError? Error,
    int? ProviderErrorCode)
{
    /// <summary>Tokenを含まない成功結果を生成します。</summary>
    public static ApiTokenStoreResult Success() => new(true, null, null, null);

    /// <summary>Token取得の成功結果を生成します。</summary>
    public static ApiTokenStoreResult Success(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new(true, token, null, null);
    }

    /// <summary>失敗結果を生成します。</summary>
    public static ApiTokenStoreResult Failure(ApiTokenStoreError error, int? providerErrorCode = null) =>
        new(false, null, error, providerErrorCode);
}
