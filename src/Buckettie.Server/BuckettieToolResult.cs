using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;

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

/// <summary>内部Gateway結果をMCP共通形式へ変換します。</summary>
internal static class BuckettieToolResultMapper
{
    internal static async Task<BuckettieToolResult<BuckettieGitData>> MapGitAsync(
        Task<GitGatewayResult> operation)
    {
        GitGatewayResult result = await operation.ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return new(true, result.Operation, result.Repository, new(result.Branch, result.Status), null);
        }

        string code = GitCode(result.Error ?? GitGatewayError.GitFailed);
        return new(false, result.Operation, result.Repository, null, CreateError(code));
    }

    internal static async Task<BuckettieToolResult<T>> MapBitbucketAsync<T>(
        Task<BitbucketResult<T>> operation,
        string operationName,
        string repository)
    {
        BitbucketResult<T> result = await operation.ConfigureAwait(false);
        if (result.IsSuccess && result.Value is not null)
        {
            return new(true, operationName, repository, result.Value, null);
        }

        string code = BitbucketCode(result.Error ?? BitbucketError.ApiError, operationName);
        return new(false, operationName, repository, default, CreateError(code));
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

    private static string NotFoundCode(string operation) => operation switch
    {
        "branch_get" => "branch_not_found",
        "pr_get" or "pr_diff" or "pr_merge" => "pull_request_not_found",
        "tag_get" => "tag_not_found",
        _ => "repository_not_found",
    };

    private static BuckettieToolError CreateError(string code) => new(code, code switch
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
        _ => "The Bitbucket API operation failed.",
    });
}
