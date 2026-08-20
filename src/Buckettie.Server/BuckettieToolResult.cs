using Buckettie.Application.Bitbucket;
using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;

namespace Buckettie.Server;

/// <summary>MCP Toolの共通エラーです。</summary>
public sealed record BuckettieToolError(string Code, string Message);

/// <summary>MCP Toolの共通構造化結果です。</summary>
public sealed record BuckettieToolResult<T>(
    bool Ok,
    string Operation,
    string Repository,
    T? Data,
    BuckettieToolError? Error);

/// <summary>Git Toolの成功データです。</summary>
public sealed record BuckettieGitData(string? Branch, GitRepositoryStatus? Status);

/// <summary>get_version Toolの成功データです。</summary>
public sealed record BuckettieVersionData(string Version);

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
        string language = "en-US")
    {
        GitGatewayResult result = await operation.ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return new(true, result.Operation, result.Repository, new(result.Branch, result.Status), null);
        }

        string code = GitCode(result.Error ?? GitGatewayError.GitFailed);
        return new(false, result.Operation, result.Repository, null, CreateError(code, language));
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
        return new(false, operationName, repository, default, CreateError(code, language));
    }

    internal static string GitCode(GitGatewayError error) => error switch
    {
        GitGatewayError.RepositoryNotAllowed => "repository_not_allowed",
        GitGatewayError.LocalRepositoryInvalid => "local_repository_invalid",
        GitGatewayError.RemoteMismatch => "remote_mismatch",
        GitGatewayError.GitNotFound => "git_not_found",
        GitGatewayError.GitFailed => "git_failed",
        GitGatewayError.WorkingTreeDirty => "working_tree_dirty",
        GitGatewayError.BranchNotAllowed => "branch_not_allowed",
        GitGatewayError.ProtectedBranch => "protected_branch",
        GitGatewayError.NothingToPush => "nothing_to_push",
        GitGatewayError.NonFastForward => "non_fast_forward",
        GitGatewayError.Timeout => "timeout",
        GitGatewayError.Cancelled => "cancelled",
        _ => "git_failed",
    };

    internal static string BitbucketCode(BitbucketError error, string operation) => error switch
    {
        BitbucketError.RepositoryNotAllowed => "repository_not_allowed",
        BitbucketError.InvalidBranch => "branch_not_allowed",
        BitbucketError.InvalidPullRequest => "pull_request_invalid",
        BitbucketError.PullRequestNotOpen => "pull_request_not_open",
        BitbucketError.PullRequestRouteNotAllowed => "pull_request_route_not_allowed",
        BitbucketError.PullRequestMergeConflict => "pull_request_merge_conflict",
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

    private static string NotFoundCode(string operation) => operation switch
    {
        "branch_get" => "branch_not_found",
        "pr_get" or "pr_diff" or "pr_merge" => "pull_request_not_found",
        "tag_get" => "tag_not_found",
        _ => "repository_not_found",
    };

    internal static BuckettieToolError Localize(BuckettieToolError error, string language) =>
        CreateError(error.Code, language);

    private static BuckettieToolError CreateError(string code, string language = "en-US") =>
        new(code, BuckettieLanguage.IsJapanese(language) ? JapaneseMessage(code) : EnglishMessage(code));

    private static string EnglishMessage(string code) => code switch
    {
        "repository_not_found" => "The repository was not found.",
        "repository_not_allowed" => "The repository is not allowed.",
        "local_repository_invalid" => "The local repository boundary is invalid.",
        "remote_mismatch" => "The configured Git remote does not match the repository.",
        "git_not_found" => "Git was not found.",
        "git_failed" => "The Git operation failed.",
        "working_tree_dirty" => "The working tree must be clean.",
        "branch_not_allowed" => "The branch is not allowed.",
        "branch_not_found" => "The branch was not found.",
        "protected_branch" => "Direct push to the protected branch is not allowed.",
        "nothing_to_push" => "There is nothing to push.",
        "non_fast_forward" => "The operation is not a fast-forward.",
        "authentication_failed" => "Bitbucket authentication failed.",
        "permission_denied" => "Bitbucket denied permission for the operation.",
        "rate_limited" => "Bitbucket rate-limited the operation.",
        "pull_request_invalid" => "The pull request input is invalid.",
        "pull_request_not_found" => "The pull request was not found.",
        "pull_request_not_open" => "The pull request is not open.",
        "pull_request_route_not_allowed" => "The pull request route is not allowed.",
        "pull_request_merge_conflict" => "The pull request cannot be merged due to a conflict.",
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
        _ => "The Bitbucket API operation failed.",
    };

    private static string JapaneseMessage(string code) => code switch
    {
        "repository_not_found" => "リポジトリが見つかりません。",
        "repository_not_allowed" => "このリポジトリは許可されていません。",
        "local_repository_invalid" => "ローカルリポジトリの境界が無効です。",
        "remote_mismatch" => "設定されたGitリモートがリポジトリと一致しません。",
        "git_not_found" => "Gitが見つかりません。",
        "git_failed" => "Git操作に失敗しました。",
        "working_tree_dirty" => "作業ツリーをクリーンな状態にしてください。",
        "branch_not_allowed" => "このブランチは許可されていません。",
        "branch_not_found" => "ブランチが見つかりません。",
        "protected_branch" => "保護ブランチへの直接pushは許可されていません。",
        "nothing_to_push" => "pushする変更がありません。",
        "non_fast_forward" => "fast-forwardできないため操作を完了できません。",
        "authentication_failed" => "Bitbucketの認証に失敗しました。",
        "permission_denied" => "Bitbucketで操作権限が拒否されました。",
        "rate_limited" => "Bitbucket APIの利用制限に達しました。",
        "pull_request_invalid" => "プルリクエストの入力が無効です。",
        "pull_request_not_found" => "プルリクエストが見つかりません。",
        "pull_request_not_open" => "プルリクエストはオープン状態ではありません。",
        "pull_request_route_not_allowed" => "このプルリクエスト経路は許可されていません。",
        "pull_request_merge_conflict" => "競合があるためプルリクエストをマージできません。",
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
        _ => "Bitbucket API操作に失敗しました。",
    };
}
