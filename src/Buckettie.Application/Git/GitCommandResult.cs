namespace Buckettie.Application.Git;

/// <summary>
/// 固定Gitコマンドの実行失敗種別です。
/// </summary>
public enum GitCommandFailure
{
    NotFound,
    Failed,
    TimedOut,
    Cancelled,
    ReferenceNotFound,
}

/// <summary>
/// 固定Gitコマンドの実行結果を表します。
/// </summary>
public sealed record GitCommandResult(
    bool IsSuccess,
    string StandardOutput,
    string StandardError,
    GitCommandFailure? Failure)
{
    /// <summary>成功結果を生成します。</summary>
    public static GitCommandResult Success(string standardOutput = "", string standardError = "") =>
        new(true, standardOutput, standardError, null);

    /// <summary>失敗結果を生成します。</summary>
    public static GitCommandResult Failed(
        GitCommandFailure failure,
        string standardError = "") =>
        new(false, string.Empty, standardError, failure);
}
