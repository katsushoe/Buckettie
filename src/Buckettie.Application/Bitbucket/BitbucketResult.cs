namespace Buckettie.Application.Bitbucket;

/// <summary>Bitbucket REST操作のエラーです。</summary>
public enum BitbucketError
{
    RepositoryNotAllowed,
    InvalidBranch,
    TokenUnavailable,
    AuthenticationFailed,
    PermissionDenied,
    NotFound,
    RateLimited,
    InvalidResponse,
    NetworkError,
    Timeout,
    Cancelled,
    ApiError,
}

/// <summary>Bitbucket REST操作の結果です。</summary>
public sealed record BitbucketResult<T>(T? Value, BitbucketError? Error)
{
    /// <summary>操作が成功したかを示します。</summary>
    public bool IsSuccess => Error is null;

    /// <summary>成功結果を生成します。</summary>
    public static BitbucketResult<T> Success(T value) => new(value, null);

    /// <summary>失敗結果を生成します。</summary>
    public static BitbucketResult<T> Failure(BitbucketError error) => new(default, error);
}
