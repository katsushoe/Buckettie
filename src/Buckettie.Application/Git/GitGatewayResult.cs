namespace Buckettie.Application.Git;

/// <summary>
/// Git Gatewayの構造化エラーコードです。
/// </summary>
public enum GitGatewayError
{
    RepositoryNotAllowed,
    LocalRepositoryInvalid,
    RemoteMismatch,
    SshRemoteNotSupported,
    GitNotFound,
    GitFailed,
    AuthenticationFailed,
    NetworkError,
    PermissionDenied,
    Conflict,
    ReferenceNotFound,
    InvalidReference,
    WorkingTreeDirty,
    BranchNotAllowed,
    ProtectedBranch,
    NothingToPush,
    NothingToCommit,
    InvalidCommitMessage,
    NonFastForward,
    Timeout,
    Cancelled,
}

/// <summary>
/// Repository状態を表します。
/// </summary>
public sealed record GitRepositoryStatus(
    string Repository,
    string LocalBranch,
    string LocalHead,
    string? RemoteDevelopHead,
    string? RemoteMainHead,
    int? Ahead,
    int? Behind,
    bool WorkingTreeClean,
    string? ComparisonReference = null,
    string? ComparisonUnavailableReason = null,
    IReadOnlyList<string>? MissingRemoteReferences = null);

/// <summary>
/// Git Gateway操作の結果を表します。
/// </summary>
public sealed record GitGatewayResult(
    bool IsSuccess,
    string Operation,
    string Repository,
    string? Branch,
    GitRepositoryStatus? Status,
    GitGatewayError? Error,
    string? CorrelationId = null,
    string? Diff = null,
    string? CommitHash = null)
{
    /// <summary>成功結果を生成します。</summary>
    public static GitGatewayResult Success(
        string operation,
        string repository,
        string? branch = null,
        GitRepositoryStatus? status = null,
        string? diff = null,
        string? commitHash = null) =>
        new(true, operation, repository, branch, status, null, null, diff, commitHash);

    /// <summary>失敗結果を生成します。</summary>
    public static GitGatewayResult Failure(
        string operation,
        string repository,
        GitGatewayError error,
        string? branch = null) =>
        new(false, operation, repository, branch, null, error, Guid.NewGuid().ToString("N"));

    /// <summary>診断相関IDを伴う失敗結果を生成します。</summary>
    public static GitGatewayResult DiagnosticFailure(
        string operation,
        string repository,
        GitGatewayError error,
        string? branch = null) =>
        Failure(operation, repository, error, branch);
}
