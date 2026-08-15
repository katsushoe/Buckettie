using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using Buckettie.Domain;

namespace Buckettie.Application.Git;

/// <summary>
/// AllowlistとRepository Policyを適用するGit Gatewayです。
/// </summary>
public sealed class GitGateway : IGitGateway
{
    private readonly RepositoryAllowlist _allowlist;
    private readonly LocalPathValidator _pathValidator;
    private readonly BitbucketRemoteUrlValidator _remoteValidator;
    private readonly IGitCommandClient _git;

    /// <summary>Git Gatewayを初期化します。</summary>
    public GitGateway(
        RepositoryAllowlist allowlist,
        LocalPathValidator pathValidator,
        BitbucketRemoteUrlValidator remoteValidator,
        IGitCommandClient git)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(pathValidator);
        ArgumentNullException.ThrowIfNull(remoteValidator);
        ArgumentNullException.ThrowIfNull(git);
        _allowlist = allowlist;
        _pathValidator = pathValidator;
        _remoteValidator = remoteValidator;
        _git = git;
    }

    /// <inheritdoc />
    public async Task<GitGatewayResult> GetStatusAsync(
        string repository,
        CancellationToken cancellationToken = default)
    {
        const string operation = "status";
        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken).ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null)
        {
            return boundary.Failure!;
        }

        GitCommandResult branch = await _git.GetCurrentBranchAsync(
            boundary.Repository.LocalRoot,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? branchError = MapCommandFailure(operation, repository, branch);
        if (branchError is not null)
        {
            return branchError;
        }

        GitCommandResult head = await _git.GetHeadAsync(
            boundary.Repository.LocalRoot,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? headError = MapCommandFailure(operation, repository, head);
        if (headError is not null)
        {
            return headError;
        }

        GitCommandResult status = await _git.GetStatusAsync(
            boundary.Repository.LocalRoot,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? statusError = MapCommandFailure(operation, repository, status);
        if (statusError is not null)
        {
            return statusError;
        }

        string branchName = branch.StandardOutput.Trim();
        GitRepositoryStatus repositoryStatus = new(
            repository,
            branchName,
            head.StandardOutput.Trim(),
            string.IsNullOrWhiteSpace(status.StandardOutput));
        return GitGatewayResult.Success(operation, repository, branchName, repositoryStatus);
    }

    /// <inheritdoc />
    public async Task<GitGatewayResult> FetchAsync(
        string repository,
        CancellationToken cancellationToken = default)
    {
        const string operation = "fetch";
        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken).ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null)
        {
            return boundary.Failure!;
        }

        GitCommandResult result = await _git.FetchAsync(
            boundary.Repository.LocalRoot,
            boundary.Repository.Remote,
            repository,
            cancellationToken).ConfigureAwait(false);
        return MapCommandFailure(operation, repository, result)
            ?? GitGatewayResult.Success(operation, repository);
    }

    /// <inheritdoc />
    public async Task<GitGatewayResult> PullAsync(
        string repository,
        CancellationToken cancellationToken = default)
    {
        const string operation = "pull";
        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken).ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null)
        {
            return boundary.Failure!;
        }

        GitCommandResult branchResult = await _git.GetCurrentBranchAsync(
            boundary.Repository.LocalRoot,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? branchError = MapCommandFailure(operation, repository, branchResult);
        if (branchError is not null)
        {
            return branchError;
        }

        string branch = branchResult.StandardOutput.Trim();
        if (!boundary.Repository.PullBranches.Contains(branch))
        {
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.BranchNotAllowed, branch);
        }

        GitCommandResult result = await _git.PullFastForwardOnlyAsync(
            boundary.Repository.LocalRoot,
            boundary.Repository.Remote,
            branch,
            repository,
            cancellationToken).ConfigureAwait(false);
        return MapCommandFailure(operation, repository, result, branch)
            ?? GitGatewayResult.Success(operation, repository, branch);
    }

    /// <inheritdoc />
    public async Task<GitGatewayResult> PushAsync(
        string repository,
        CancellationToken cancellationToken = default)
    {
        const string operation = "push";
        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken).ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null)
        {
            return boundary.Failure!;
        }

        GitCommandResult branchResult = await _git.GetCurrentBranchAsync(
            boundary.Repository.LocalRoot,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? branchError = MapCommandFailure(operation, repository, branchResult);
        if (branchError is not null)
        {
            return branchError;
        }

        GitCommandResult statusResult = await _git.GetStatusAsync(
            boundary.Repository.LocalRoot,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? statusError = MapCommandFailure(operation, repository, statusResult);
        if (statusError is not null)
        {
            return statusError;
        }

        string branch = branchResult.StandardOutput.Trim();
        RepositoryPolicy policy = CreatePolicy(repository, boundary.Repository);
        PolicyResult policyResult = policy.ValidatePush(branch, string.IsNullOrWhiteSpace(statusResult.StandardOutput));
        if (!policyResult.IsAllowed)
        {
            return GitGatewayResult.Failure(operation, repository, MapPolicyError(policyResult.ErrorCode), branch);
        }

        GitCommandResult result = await _git.PushAsync(
            boundary.Repository.LocalRoot,
            boundary.Repository.Remote,
            branch,
            repository,
            cancellationToken).ConfigureAwait(false);
        return MapCommandFailure(operation, repository, result, branch)
            ?? GitGatewayResult.Success(operation, repository, branch);
    }

    private async Task<BoundaryResult> ValidateBoundaryAsync(
        string repository,
        string operation,
        CancellationToken cancellationToken)
    {
        if (!RepositoryId.IsValid(repository))
        {
            return BoundaryResult.Invalid(GitGatewayResult.Failure(
                operation,
                repository ?? string.Empty,
                GitGatewayError.RepositoryNotAllowed));
        }

        if (!_allowlist.TryGet(repository, out RepositoryOptions? options) || options is null)
        {
            return BoundaryResult.Invalid(GitGatewayResult.Failure(
                operation,
                repository,
                GitGatewayError.RepositoryNotAllowed));
        }

        RepositoryValidationResult path;
        try
        {
            path = _pathValidator.Validate(options.LocalRoot, options.LocalRoot);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return BoundaryResult.Invalid(GitGatewayResult.Failure(
                operation,
                repository,
                GitGatewayError.LocalRepositoryInvalid));
        }
        if (!path.IsValid)
        {
            return BoundaryResult.Invalid(GitGatewayResult.Failure(
                operation,
                repository,
                GitGatewayError.LocalRepositoryInvalid));
        }

        GitCommandResult remote = await _git.GetRemoteUrlAsync(
            options.LocalRoot,
            options.Remote,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? commandError = MapCommandFailure(operation, repository, remote);
        if (commandError is not null)
        {
            return BoundaryResult.Invalid(commandError);
        }

        RepositoryValidationResult remoteValidation = _remoteValidator.Validate(
            options.Workspace,
            options.Slug,
            remote.StandardOutput.Trim());
        return remoteValidation.IsValid
            ? BoundaryResult.Valid(options)
            : BoundaryResult.Invalid(GitGatewayResult.Failure(
                operation,
                repository,
                GitGatewayError.RemoteMismatch));
    }

    private static GitGatewayResult? MapCommandFailure(
        string operation,
        string repository,
        GitCommandResult result,
        string? branch = null)
    {
        if (result.IsSuccess)
        {
            if (operation == "push"
                && result.StandardError.Contains("Everything up-to-date", StringComparison.Ordinal))
            {
                return GitGatewayResult.Failure(operation, repository, GitGatewayError.NothingToPush, branch);
            }

            return null;
        }

        GitGatewayError error = result.Failure switch
        {
            GitCommandFailure.NotFound => GitGatewayError.GitNotFound,
            GitCommandFailure.TimedOut => GitGatewayError.Timeout,
            GitCommandFailure.Cancelled => GitGatewayError.Cancelled,
            _ when result.StandardError.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase) =>
                GitGatewayError.NonFastForward,
            _ => GitGatewayError.GitFailed,
        };
        return GitGatewayResult.Failure(operation, repository, error, branch);
    }

    private static RepositoryPolicy CreatePolicy(string repository, RepositoryOptions options) => new(
        repository,
        options.DevelopBranch,
        options.MainBranch,
        options.DirectPushBranches,
        options.PullBranches,
        new HashSet<PullRequestRoute>(),
        options.ProtectedBranches,
        options.TagTargetBranch,
        options.TagPattern,
        options.RequireCleanWorkingTree);

    private static GitGatewayError MapPolicyError(PolicyErrorCode? error) => error switch
    {
        PolicyErrorCode.WorkingTreeDirty => GitGatewayError.WorkingTreeDirty,
        PolicyErrorCode.ProtectedBranch => GitGatewayError.ProtectedBranch,
        _ => GitGatewayError.BranchNotAllowed,
    };

    private sealed record BoundaryResult(
        bool IsValid,
        RepositoryOptions? Repository,
        GitGatewayResult? Failure)
    {
        internal static BoundaryResult Valid(RepositoryOptions repository) => new(true, repository, null);

        internal static BoundaryResult Invalid(GitGatewayResult failure) => new(false, null, failure);
    }
}
