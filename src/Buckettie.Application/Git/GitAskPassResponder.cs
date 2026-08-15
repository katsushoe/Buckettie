using Buckettie.Application.Credentials;

namespace Buckettie.Application.Git;

/// <summary>
/// Git AskPassのUsername／Password要求へ応答します。
/// </summary>
public sealed class GitAskPassResponder
{
    private readonly IApiTokenStore _tokenStore;

    /// <summary>Responderを初期化します。</summary>
    public GitAskPassResponder(IApiTokenStore tokenStore)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Git promptに対応する値を返します。
    /// </summary>
    public GitAskPassResponse Respond(string repositoryId, string username, string prompt)
    {
        if (string.IsNullOrWhiteSpace(repositoryId)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(prompt))
        {
            return GitAskPassResponse.Failure(GitAskPassError.InvalidRequest);
        }

        if (prompt.StartsWith("Username", StringComparison.OrdinalIgnoreCase))
        {
            return GitAskPassResponse.Success(username);
        }

        if (!prompt.StartsWith("Password", StringComparison.OrdinalIgnoreCase))
        {
            return GitAskPassResponse.Failure(GitAskPassError.UnsupportedPrompt);
        }

        ApiTokenStoreResult token = _tokenStore.Read(repositoryId);
        return token.IsSuccess && token.Token is not null
            ? GitAskPassResponse.Success(token.Token)
            : GitAskPassResponse.Failure(GitAskPassError.TokenUnavailable);
    }
}

/// <summary>AskPass応答エラーです。</summary>
public enum GitAskPassError
{
    InvalidRequest,
    UnsupportedPrompt,
    TokenUnavailable,
}

/// <summary>AskPass応答結果です。</summary>
public sealed record GitAskPassResponse(bool IsSuccess, string? Value, GitAskPassError? Error)
{
    /// <summary>成功結果を生成します。</summary>
    public static GitAskPassResponse Success(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new(true, value, null);
    }

    /// <summary>失敗結果を生成します。</summary>
    public static GitAskPassResponse Failure(GitAskPassError error) => new(false, null, error);
}
