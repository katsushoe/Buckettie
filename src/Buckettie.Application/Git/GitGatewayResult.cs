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
    ExpectedHeadMismatch,
    HistoryRewriteNotAllowed,
    BranchNotCheckedOut,
    UnfinishedOperation,
    InvalidIdentity,
    NoIdentityChange,
    SignedCommitConfirmationRequired,
    RemoteVerificationFailed,
}

/// <summary>Git commitの氏名とメールアドレスです。</summary>
public sealed record GitIdentity(string Name, string Email);

/// <summary>最新commitのidentity書き換え要求です。</summary>
public sealed record GitHistoryRewriteRequest(
    string Branch,
    string ExpectedOldHead,
    string Reason,
    string? AuthorName = null,
    string? AuthorEmail = null,
    string? CommitterName = null,
    string? CommitterEmail = null,
    bool AllowSignatureRemoval = false);

/// <summary>履歴書き換えの事前確認および実行結果です。</summary>
public sealed record GitHistoryRewriteData(
    string Remote,
    string Branch,
    string OldHead,
    string? NewHead,
    GitIdentity AuthorBefore,
    GitIdentity AuthorAfter,
    GitIdentity CommitterBefore,
    GitIdentity CommitterAfter,
    string AuthorDate,
    string CommitterDate,
    bool DatesPreserved,
    bool WasSigned,
    bool SignatureWillBeRemoved,
    bool RemoteUpdateRequired,
    string? RecoveryReference,
    bool RemoteUpdated);

/// <summary>force-with-lease要求です。</summary>
public sealed record GitForceWithLeaseRequest(
    string Branch,
    string ExpectedLocalHead,
    string ExpectedRemoteHead,
    string Reason);

/// <summary>force-with-lease結果です。</summary>
public sealed record GitForceWithLeaseData(
    string Remote,
    string Branch,
    string ExpectedRemoteHead,
    string NewLocalHead,
    string VerifiedRemoteHead,
    bool RemoteUpdated);

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
    string? CommitHash = null,
    string? ErrorDetail = null,
    GitHistoryRewriteData? HistoryRewrite = null,
    GitForceWithLeaseData? ForceWithLease = null)
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
        string? branch = null,
        string? errorDetail = null) =>
        new(false, operation, repository, branch, null, error, Guid.NewGuid().ToString("N"),
            ErrorDetail: errorDetail);
}
