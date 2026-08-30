using Buckettie.Application.Bitbucket;
using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;
using System.Text.Json.Serialization;

namespace Buckettie.Server;

/// <summary>MCP Toolの共通エラーです。</summary>
public sealed record BuckettieToolError(
    [property: JsonPropertyName("code")] string Code,
    string Message,
    string? Summary = null,
    string? SuggestedAction = null,
    [property: JsonPropertyName("retryable")] bool Retryable = false,
    string? CorrelationId = null,
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("retry_after_seconds")] int? RetryAfterSeconds = null,
    [property: JsonPropertyName("project_candidates")] IReadOnlyList<string>? ProjectCandidates = null);

/// <summary>MCP Toolの共通構造化結果です。</summary>
public sealed record BuckettieToolResult<T>(
    bool Ok,
    string Operation,
    string Repository,
    T? Data,
    BuckettieToolError? Error);

/// <summary>Git Toolの成功データです。</summary>
public sealed record BuckettieGitData(
    string? Branch,
    GitRepositoryStatus? Status,
    string? Diff,
    string? CommitHash);

/// <summary>get_version Toolの成功データです。</summary>
public sealed record BuckettieVersionData(string Version);

/// <summary>list_projects Toolの成功データです。</summary>
public sealed record BuckettieProjectListData(IReadOnlyList<string> Projects);

/// <summary>bitbucket_repository_register Toolの成功データです。</summary>
public sealed record BuckettieRepositoryRegistrationData(
    string RepositoryId,
    string Workspace,
    string Slug,
    bool Approved);

/// <summary>bitbucket_repository_unregister Toolの成功データです。</summary>
public sealed record BuckettieRepositoryUnregistrationData(string RepositoryId);

/// <summary>bitbucket_repository_update Toolの成功データです。</summary>
public sealed record BuckettieRepositoryUpdateData(string RepositoryId, bool Approved);

/// <summary>内部Gateway結果をMCP共通形式へ変換します。</summary>
internal static class BuckettieToolResultMapper
{
    internal static async Task<BuckettieToolResult<BuckettieGitData>> MapGitAsync(
        Task<GitGatewayResult> operation,
        string language = "en-US",
        IReadOnlyList<string>? projectCandidates = null)
    {
        GitGatewayResult result = await operation.ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return new(true, result.Operation, result.Repository,
                new(result.Branch, result.Status, result.Diff, result.CommitHash), null);
        }

        string code = GitCode(result.Error ?? GitGatewayError.GitFailed);
        return new(false, result.Operation, result.Repository, null,
            CreateGitError(code, language, result.Operation, result.CorrelationId, projectCandidates));
    }

    internal static async Task<BuckettieToolResult<T>> MapBitbucketAsync<T>(
        Task<BitbucketResult<T>> operation,
        string operationName,
        string repository,
        string language = "en-US")
    {
        BitbucketResult<T> result = await operation.ConfigureAwait(false);
        if (result.IsSuccess && result.Value is not null)
        {
            return new(true, operationName, repository, result.Value, null);
        }

        string code = BitbucketCode(result.Error ?? BitbucketError.ApiError, operationName);
        return new(false, operationName, repository, default,
            CreateBitbucketError(code, operationName, language));
    }

    internal static string GitCode(GitGatewayError error) => error switch
    {
        GitGatewayError.RepositoryNotAllowed => "repository_not_allowed",
        GitGatewayError.LocalRepositoryInvalid => "local_repository_invalid",
        GitGatewayError.RemoteMismatch => "remote_mismatch",
        GitGatewayError.SshRemoteNotSupported => "ssh_remote_not_supported",
        GitGatewayError.GitNotFound => "git_not_found",
        GitGatewayError.GitFailed => "git_failed",
        GitGatewayError.AuthenticationFailed => "authentication_failed",
        GitGatewayError.NetworkError => "network_error",
        GitGatewayError.PermissionDenied => "permission_denied",
        GitGatewayError.Conflict => "conflict",
        GitGatewayError.ReferenceNotFound => "tag_not_found",
        GitGatewayError.InvalidReference => "tag_invalid",
        GitGatewayError.WorkingTreeDirty => "working_tree_dirty",
        GitGatewayError.BranchNotAllowed => "branch_not_allowed",
        GitGatewayError.ProtectedBranch => "protected_branch",
        GitGatewayError.NothingToPush => "nothing_to_push",
        GitGatewayError.NothingToCommit => "nothing_to_commit",
        GitGatewayError.InvalidCommitMessage => "commit_message_invalid",
        GitGatewayError.NonFastForward => "non_fast_forward",
        GitGatewayError.Timeout => "timeout",
        GitGatewayError.Cancelled => "cancelled",
        _ => "git_failed",
    };

    internal static string BitbucketCode(BitbucketError error, string operation) => error switch
    {
        BitbucketError.RepositoryNotAllowed => "repository_not_allowed",
        BitbucketError.InvalidBranch => "branch_not_allowed",
        BitbucketError.BranchAlreadyExists => "branch_already_exists",
        BitbucketError.BranchProtected => "protected_branch",
        BitbucketError.InvalidPullRequest => "pull_request_invalid",
        BitbucketError.PullRequestNotOpen => "pull_request_not_open",
        BitbucketError.PullRequestRouteNotAllowed => "pull_request_route_not_allowed",
        BitbucketError.PullRequestMergeConflict => "pull_request_merge_conflict",
        BitbucketError.PullRequestMergeBlocked => "pull_request_merge_blocked",
        BitbucketError.MergeabilityCalculating => "mergeability_calculating",
        BitbucketError.MergeabilityUnknown => "mergeability_unknown",
        BitbucketError.InvalidTag => "tag_invalid",
        BitbucketError.TagAlreadyExists => "tag_already_exists",
        BitbucketError.TagTargetNotAllowed => "tag_target_not_allowed",
        BitbucketError.TokenUnavailable => "authentication_failed",
        BitbucketError.AuthenticationFailed => "authentication_failed",
        BitbucketError.PermissionDenied => "permission_denied",
        BitbucketError.NotFound => NotFoundCode(operation),
        BitbucketError.RateLimited => "rate_limited",
        BitbucketError.NetworkError => "network_error",
        BitbucketError.Timeout => "timeout",
        BitbucketError.Cancelled => "cancelled",
        BitbucketError.InvalidResponse => "bitbucket_api_error",
        BitbucketError.ApiError => "bitbucket_api_error",
        _ => "bitbucket_api_error",
    };

    internal static BuckettieToolError RegistrationValidationError(RepositoryValidationError error) =>
        CreateError(error switch
        {
            RepositoryValidationError.RepositoryIdInvalid => "repository_id_invalid",
            RepositoryValidationError.RepositoryAlreadyRegistered => "repository_already_registered",
            RepositoryValidationError.RepositoryNotRegistered => "repository_not_registered",
            RepositoryValidationError.RemoteUrlInvalid => "remote_url_invalid",
            RepositoryValidationError.SshRemoteNotSupported => "ssh_remote_not_supported",
            RepositoryValidationError.TagPatternInvalid => "tag_pattern_invalid",
            RepositoryValidationError.LocalRootNotFound
                or RepositoryValidationError.GitMetadataNotFound
                or RepositoryValidationError.LocalPathReparsePoint => "local_repository_invalid",
            _ => "local_repository_invalid",
        });

    internal static BuckettieToolError RegistrationApprovalError(ApprovalOutcome outcome) =>
        CreateError(outcome switch
        {
            ApprovalOutcome.Denied => "approval_denied",
            ApprovalOutcome.TimedOut => "approval_timed_out",
            ApprovalOutcome.NoInteractiveSession => "no_interactive_session",
            _ => "approval_launch_failed",
        });

    internal static BuckettieToolError RegistrationInProgressError() => CreateError("registration_in_progress");

    internal static BuckettieToolError RegistrationWriteFailedError() => CreateError("registration_write_failed");

    internal static BuckettieToolError RegistrationTokenFailedError() => CreateError("registration_token_failed");

    private static string NotFoundCode(string operation) => operation switch
    {
        "branch_get" or "branch_delete" => "branch_not_found",
        "pr_get" or "pr_diff" or "pr_merge" => "pull_request_not_found",
        "tag_get" or "tag_delete" => "tag_not_found",
        _ => "repository_not_found",
    };

    internal static BuckettieToolError Localize(BuckettieToolError error, string language) =>
        CreateError(error.Code, language);

    private static BuckettieToolError CreateGitError(
        string code,
        string language,
        string operation,
        string? correlationId,
        IReadOnlyList<string>? projectCandidates)
    {
        bool japanese = BuckettieLanguage.IsJapanese(language);
        string message = japanese ? JapaneseMessage(code) : EnglishMessage(code);
        return new(code, message, $"Git {operation}: {message}",
            japanese ? JapaneseAction(code) : EnglishAction(code), IsRetryable(code), correlationId,
            ProjectCandidates: code == "repository_not_allowed" ? projectCandidates : null);
    }

    private static BuckettieToolError CreateError(string code, string language = "en-US") =>
        new(code, BuckettieLanguage.IsJapanese(language) ? JapaneseMessage(code) : EnglishMessage(code));

    private static BuckettieToolError CreateBitbucketError(
        string code,
        string operation,
        string language)
    {
        string message = BuckettieLanguage.IsJapanese(language) ? JapaneseMessage(code) : EnglishMessage(code);
        if (!string.Equals(operation, "pr_merge", StringComparison.Ordinal))
        {
            return new(code, message);
        }

        return code switch
        {
            "mergeability_calculating" => new(
                code, message, Retryable: true, Status: "calculating_retryable", RetryAfterSeconds: 2),
            "mergeability_unknown" => new(
                code, message, Retryable: true, Status: "unknown_retryable", RetryAfterSeconds: 2),
            "pull_request_merge_conflict" => new(code, message, Status: "conflicting"),
            "pull_request_merge_blocked" or "pull_request_route_not_allowed" or "pull_request_not_open" =>
                new(code, message, Status: "blocked"),
            _ => new(code, message),
        };
    }

    private static bool IsRetryable(string code) => code is "network_error" or "timeout" or "rate_limited";

    private static string EnglishAction(string code) => code switch
    {
        "authentication_failed" => "Verify the repository credential, then retry.",
        "network_error" => "Check DNS and network connectivity, then retry.",
        "permission_denied" => "Verify repository permissions before retrying.",
        "conflict" => "Resolve the Git conflict locally, then retry.",
        "non_fast_forward" => "Fetch and integrate the remote changes before retrying.",
        "working_tree_dirty" => "Commit or stash local changes before retrying.",
        "remote_mismatch" => "Verify the configured remote and repository registration.",
        "git_failed" => "Use the correlation ID to inspect the Buckettie audit log.",
        "timeout" => "Check connectivity and retry the operation.",
        _ => "Correct the reported condition before retrying.",
    };

    private static string JapaneseAction(string code) => code switch
    {
        "authentication_failed" => "リポジトリの認証情報を確認してから再試行してください。",
        "network_error" => "DNSとネットワーク接続を確認してから再試行してください。",
        "permission_denied" => "リポジトリ権限を確認してから再試行してください。",
        "conflict" => "Git競合をローカルで解消してから再試行してください。",
        "non_fast_forward" => "リモート変更をfetchして統合してから再試行してください。",
        "working_tree_dirty" => "ローカル変更をcommitまたはstashしてから再試行してください。",
        "remote_mismatch" => "設定済みリモートとリポジトリ登録を確認してください。",
        "git_failed" => "相関IDを使用してBuckettie監査ログを確認してください。",
        "timeout" => "接続状態を確認して操作を再試行してください。",
        _ => "報告された状態を修正してから再試行してください。",
    };

    private static string EnglishMessage(string code) => code switch
    {
        "repository_not_found" => "The repository was not found.",
        "repository_not_allowed" => "The repository is not allowed.",
        "local_repository_invalid" => "The local repository boundary is invalid.",
        "remote_mismatch" => "The configured Git remote does not match the repository.",
        "ssh_remote_not_supported" => "SSH Git remotes are not supported. Change the remote to the Bitbucket HTTPS URL.",
        "git_not_found" => "Git was not found.",
        "git_failed" => "The Git operation failed.",
        "conflict" => "The Git operation encountered a conflict.",
        "working_tree_dirty" => "The working tree must be clean.",
        "branch_not_allowed" => "The branch is not allowed.",
        "branch_not_found" => "The branch was not found.",
        "branch_already_exists" => "The branch already exists.",
        "protected_branch" => "Direct push to the protected branch is not allowed.",
        "nothing_to_push" => "There is nothing to push.",
        "nothing_to_commit" => "There are no changes to commit.",
        "commit_message_invalid" => "The commit message is invalid.",
        "non_fast_forward" => "The operation is not a fast-forward.",
        "authentication_failed" => "Bitbucket authentication failed.",
        "permission_denied" => "Bitbucket denied permission for the operation.",
        "rate_limited" => "Bitbucket rate-limited the operation.",
        "pull_request_invalid" => "The pull request input is invalid.",
        "pull_request_not_found" => "The pull request was not found.",
        "pull_request_not_open" => "The pull request is not open.",
        "pull_request_route_not_allowed" => "The pull request route is not allowed.",
        "pull_request_merge_conflict" => "The pull request cannot be merged due to a conflict.",
        "pull_request_merge_blocked" => "The pull request is blocked by repository requirements.",
        "mergeability_calculating" => "Mergeability is still being calculated.",
        "mergeability_unknown" => "Mergeability is temporarily unknown.",
        "tag_invalid" => "The tag is invalid.",
        "tag_already_exists" => "The tag already exists.",
        "tag_target_not_allowed" => "The tag target is not allowed.",
        "tag_not_found" => "The tag was not found.",
        "network_error" => "The network operation failed.",
        "timeout" => "The operation timed out.",
        "cancelled" => "The operation was cancelled.",
        "repository_id_invalid" => "The repository ID is invalid.",
        "repository_already_registered" => "The repository is already registered.",
        "repository_not_registered" => "The repository is not registered.",
        "remote_url_invalid" => "The local repository's Git remote is not a valid Bitbucket repository.",
        "approval_denied" => "The repository registration was denied.",
        "approval_timed_out" => "The repository registration approval timed out.",
        "no_interactive_session" => "No interactive desktop session is available to approve the request.",
        "approval_launch_failed" => "The approval prompt could not be launched.",
        "registration_in_progress" => "Another repository registration is already in progress.",
        "registration_write_failed" => "The repository could not be persisted to the configuration file.",
        "registration_token_failed" => "The repository API Token could not be saved.",
        _ => "The Bitbucket API operation failed.",
    };

    private static string JapaneseMessage(string code) => code switch
    {
        "repository_not_found" => "リポジトリが見つかりません。",
        "repository_not_allowed" => "このリポジトリは許可されていません。",
        "local_repository_invalid" => "ローカルリポジトリの境界が無効です。",
        "remote_mismatch" => "設定されたGitリモートがリポジトリと一致しません。",
        "ssh_remote_not_supported" => "SSH形式のGitリモートには対応していません。BitbucketのHTTPS URLへ変更してください。",
        "git_not_found" => "Gitが見つかりません。",
        "git_failed" => "Git操作に失敗しました。",
        "conflict" => "Git操作で競合が発生しました。",
        "working_tree_dirty" => "作業ツリーをクリーンな状態にしてください。",
        "branch_not_allowed" => "このブランチは許可されていません。",
        "branch_not_found" => "ブランチが見つかりません。",
        "branch_already_exists" => "ブランチは既に存在します。",
        "protected_branch" => "保護ブランチへの直接pushは許可されていません。",
        "nothing_to_push" => "pushする変更がありません。",
        "nothing_to_commit" => "commitする変更がありません。",
        "commit_message_invalid" => "commitメッセージが無効です。",
        "non_fast_forward" => "fast-forwardできないため操作を完了できません。",
        "authentication_failed" => "Bitbucketの認証に失敗しました。",
        "permission_denied" => "Bitbucketで操作権限が拒否されました。",
        "rate_limited" => "Bitbucket APIの利用制限に達しました。",
        "pull_request_invalid" => "プルリクエストの入力が無効です。",
        "pull_request_not_found" => "プルリクエストが見つかりません。",
        "pull_request_not_open" => "プルリクエストはオープン状態ではありません。",
        "pull_request_route_not_allowed" => "このプルリクエスト経路は許可されていません。",
        "pull_request_merge_conflict" => "競合があるためプルリクエストをマージできません。",
        "pull_request_merge_blocked" => "リポジトリ要件によりプルリクエストのマージが拒否されました。",
        "mergeability_calculating" => "マージ可能性を計算中です。",
        "mergeability_unknown" => "マージ可能性を一時的に判定できません。",
        "tag_invalid" => "タグが無効です。",
        "tag_already_exists" => "タグは既に存在します。",
        "tag_target_not_allowed" => "タグの対象は許可されていません。",
        "tag_not_found" => "タグが見つかりません。",
        "network_error" => "ネットワーク操作に失敗しました。",
        "timeout" => "操作がタイムアウトしました。",
        "cancelled" => "操作はキャンセルされました。",
        "repository_id_invalid" => "リポジトリIDが無効です。",
        "repository_already_registered" => "リポジトリは既に登録されています。",
        "repository_not_registered" => "リポジトリは登録されていません。",
        "remote_url_invalid" => "ローカルリポジトリのGitリモートは有効なBitbucketリポジトリではありません。",
        "approval_denied" => "リポジトリ登録は承認されませんでした。",
        "approval_timed_out" => "リポジトリ登録の承認がタイムアウトしました。",
        "no_interactive_session" => "承認に使用できる対話型デスクトップセッションがありません。",
        "approval_launch_failed" => "承認ダイアログを起動できませんでした。",
        "registration_in_progress" => "別のリポジトリ登録が進行中です。",
        "registration_write_failed" => "リポジトリを設定ファイルへ保存できませんでした。",
        "registration_token_failed" => "リポジトリのAPI Tokenを保存できませんでした。",
        _ => "Bitbucket API操作に失敗しました。",
    };
}
