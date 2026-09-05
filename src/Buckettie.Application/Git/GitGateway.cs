using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using Buckettie.Domain;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Buckettie.Application.Git;

/// <summary>
/// AllowlistとRepository Policyを適用するGit Gatewayです。
/// </summary>
public sealed class GitGateway : IGitGateway
{
    private static readonly Regex FullShaPattern = new("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant);
    private static readonly Regex BranchPattern = new("^(?!.*(?:\\.\\.|//|@\\{|\\\\))[A-Za-z0-9][A-Za-z0-9._/-]{0,254}$", RegexOptions.CultureInvariant);
    private const int MaximumErrorDetailLength = 1024;
    private static readonly Regex UrlPattern = new(
        "(?i)\\b(?:https?|ssh)://[^\\s'\\\"]+|\\b[^@\\s]+@[^:\\s]+:[^\\s]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex EmailPattern = new(
        "(?i)\\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}\\b",
        RegexOptions.CultureInvariant);
    private static readonly Regex WindowsPathPattern = new(
        "(?i)(?:[A-Z]:\\\\|\\\\\\\\)[^\\s'\\\"]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex UnixPathPattern = new(
        "(?<![A-Za-z0-9])/(?:[^\\s'\\\"]+/)*[^\\s'\\\"]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex SecretPattern = new(
        "(?i)\\b(password|token|authorization|credential)\\s*[:=]\\s*[^\\s]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex WhitespacePattern = new("\\s+", RegexOptions.CultureInvariant);
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

        GitCommandResult developHead = await _git.GetRemoteHeadAsync(
            boundary.Repository.LocalRoot,
            boundary.Repository.Remote,
            boundary.Repository.DevelopBranch,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? developHeadError = MapCommandFailure(operation, repository, developHead);
        if (developHeadError is not null && developHead.Failure != GitCommandFailure.ReferenceNotFound)
        {
            return developHeadError;
        }

        GitCommandResult mainHead = await _git.GetRemoteHeadAsync(
            boundary.Repository.LocalRoot,
            boundary.Repository.Remote,
            boundary.Repository.MainBranch,
            cancellationToken).ConfigureAwait(false);
        GitGatewayResult? mainHeadError = MapCommandFailure(operation, repository, mainHead);
        if (mainHeadError is not null && mainHead.Failure != GitCommandFailure.ReferenceNotFound)
        {
            return mainHeadError;
        }

        string comparisonReference = $"refs/remotes/{boundary.Repository.Remote}/{boundary.Repository.DevelopBranch}";
        List<string> missingReferences = [];
        int? ahead = null;
        int? behind = null;
        if (!developHead.IsSuccess)
        {
            missingReferences.Add(comparisonReference);
        }
        else
        {
            GitCommandResult divergence = await _git.GetAheadBehindAsync(
                boundary.Repository.LocalRoot, boundary.Repository.Remote,
                boundary.Repository.DevelopBranch, cancellationToken).ConfigureAwait(false);
            GitGatewayResult? divergenceError = MapCommandFailure(operation, repository, divergence);
            if (divergenceError is not null)
            {
                return divergenceError;
            }

            if (!TryParseAheadBehind(divergence.StandardOutput, out int aheadCount, out int behindCount))
            {
                return GitGatewayResult.DiagnosticFailure(operation, repository, GitGatewayError.GitFailed);
            }
            ahead = aheadCount;
            behind = behindCount;
        }
        if (!mainHead.IsSuccess)
        {
            missingReferences.Add($"refs/remotes/{boundary.Repository.Remote}/{boundary.Repository.MainBranch}");
        }

        string branchName = branch.StandardOutput.Trim();
        GitRepositoryStatus repositoryStatus = new(
            repository,
            branchName,
            head.StandardOutput.Trim(),
            developHead.IsSuccess ? developHead.StandardOutput.Trim() : null,
            mainHead.IsSuccess ? mainHead.StandardOutput.Trim() : null,
            ahead,
            behind,
            string.IsNullOrWhiteSpace(status.StandardOutput),
            comparisonReference,
            developHead.IsSuccess ? null : "remote_tracking_ref_missing_or_not_fetched",
            missingReferences.Distinct(StringComparer.Ordinal).ToArray());
        return GitGatewayResult.Success(operation, repository, branchName, repositoryStatus);
    }

    /// <inheritdoc />
    public async Task<GitGatewayResult> GetDiffAsync(
        string repository,
        CancellationToken cancellationToken = default)
    {
        const string operation = "repository_diff";
        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken)
            .ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null)
        {
            return boundary.Failure!;
        }

        GitCommandResult result = await _git.GetDiffAsync(
            boundary.Repository.LocalRoot, cancellationToken).ConfigureAwait(false);
        return MapCommandFailure(operation, repository, result)
            ?? GitGatewayResult.Success(operation, repository, diff: result.StandardOutput);
    }

    /// <inheritdoc />
    public async Task<GitGatewayResult> CommitAsync(
        string repository,
        string message,
        CancellationToken cancellationToken = default)
    {
        const string operation = "repository_commit";
        if (string.IsNullOrWhiteSpace(message) || message.Length > 4096)
        {
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.InvalidCommitMessage);
        }

        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken)
            .ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null)
        {
            return boundary.Failure!;
        }

        GitCommandResult branchResult = await _git.GetCurrentBranchAsync(
            boundary.Repository.LocalRoot, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? branchError = MapCommandFailure(operation, repository, branchResult);
        if (branchError is not null)
        {
            return branchError;
        }

        string branch = branchResult.StandardOutput.Trim();
        PolicyResult policyResult = CreatePolicy(repository, boundary.Repository).ValidatePush(branch, true);
        if (!policyResult.IsAllowed)
        {
            return GitGatewayResult.Failure(
                operation, repository, MapPolicyError(policyResult.ErrorCode), branch);
        }

        GitCommandResult statusResult = await _git.GetStatusAsync(
            boundary.Repository.LocalRoot, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? statusError = MapCommandFailure(operation, repository, statusResult, branch);
        if (statusError is not null)
        {
            return statusError;
        }
        if (string.IsNullOrWhiteSpace(statusResult.StandardOutput))
        {
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.NothingToCommit, branch);
        }

        GitCommandResult stageResult = await _git.StageAllAsync(
            boundary.Repository.LocalRoot, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? stageError = MapCommandFailure(operation, repository, stageResult, branch);
        if (stageError is not null)
        {
            return stageError;
        }

        GitCommandResult commitResult = await _git.CommitAsync(
            boundary.Repository.LocalRoot, message, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? commitError = MapCommandFailure(operation, repository, commitResult, branch);
        if (commitError is not null)
        {
            return commitError;
        }

        GitCommandResult headResult = await _git.GetHeadAsync(
            boundary.Repository.LocalRoot, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? headError = MapCommandFailure(operation, repository, headResult, branch);
        return headError ?? GitGatewayResult.Success(
            operation, repository, branch, commitHash: headResult.StandardOutput.Trim());
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

    /// <inheritdoc />
    public async Task<GitGatewayResult> PushTagAsync(
        string repository,
        string tag,
        CancellationToken cancellationToken = default)
    {
        const string operation = "tag_push";
        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken)
            .ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null)
        {
            return boundary.Failure!;
        }

        RepositoryPolicy policy = CreatePolicy(repository, boundary.Repository);
        PolicyResult policyResult = policy.ValidateTag(tag, boundary.Repository.TagTargetBranch);
        if (!policyResult.IsAllowed)
        {
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.InvalidReference, tag);
        }

        GitCommandResult result = await _git.PushTagAsync(
            boundary.Repository.LocalRoot,
            boundary.Repository.Remote,
            tag,
            repository,
            cancellationToken).ConfigureAwait(false);
        return MapCommandFailure(operation, repository, result, tag)
            ?? GitGatewayResult.Success(operation, repository, tag);
    }

    /// <inheritdoc />
    public Task<GitGatewayResult> PreviewHistoryRewriteAsync(
        string repository, GitHistoryRewriteRequest request, CancellationToken cancellationToken = default) =>
        PrepareHistoryRewriteAsync(repository, request, "history_rewrite_preview", cancellationToken);

    /// <inheritdoc />
    public async Task<GitGatewayResult> RewriteHistoryAsync(
        string repository, GitHistoryRewriteRequest request, CancellationToken cancellationToken = default)
    {
        const string operation = "history_rewrite_execute";
        GitGatewayResult prepared = await PrepareHistoryRewriteAsync(repository, request, operation, cancellationToken)
            .ConfigureAwait(false);
        if (!prepared.IsSuccess || prepared.HistoryRewrite is null)
        {
            return prepared;
        }

        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken).ConfigureAwait(false);
        RepositoryOptions options = boundary.Repository!;
        GitCommandResult metadataResult = await _git.GetCommitMetadataAsync(
            options.LocalRoot, request.ExpectedOldHead, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? metadataError = MapCommandFailure(operation, repository, metadataResult, request.Branch);
        if (metadataError is not null) return metadataError;
        string[] fields = metadataResult.StandardOutput.Split('\u001f', 11);
        if (fields.Length != 11) return GitGatewayResult.Failure(operation, repository, GitGatewayError.GitFailed, request.Branch);

        string recovery = $"refs/buckettie/recovery/{request.Branch}/{request.ExpectedOldHead}";
        GitCommandResult recoveryResult = await _git.CreateReferenceAsync(
            options.LocalRoot, recovery, request.ExpectedOldHead, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? recoveryError = MapCommandFailure(operation, repository, recoveryResult, request.Branch);
        if (recoveryError is not null) return recoveryError;

        GitHistoryRewriteData preview = prepared.HistoryRewrite;
        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["GIT_AUTHOR_NAME"] = preview.AuthorAfter.Name,
            ["GIT_AUTHOR_EMAIL"] = preview.AuthorAfter.Email,
            ["GIT_AUTHOR_DATE"] = preview.AuthorDate,
            ["GIT_COMMITTER_NAME"] = preview.CommitterAfter.Name,
            ["GIT_COMMITTER_EMAIL"] = preview.CommitterAfter.Email,
            ["GIT_COMMITTER_DATE"] = preview.CommitterDate,
        };
        GitCommandResult createResult = await _git.CreateCommitAsync(
            options.LocalRoot, $"{fields[1]}\u001f{fields[2]}\u001f{fields[10]}", environment, cancellationToken)
            .ConfigureAwait(false);
        GitGatewayResult? createError = MapCommandFailure(operation, repository, createResult, request.Branch);
        if (createError is not null) return createError;
        string newHead = createResult.StandardOutput.Trim();

        GitCommandResult updateResult = await _git.UpdateBranchReferenceAsync(
            options.LocalRoot, request.Branch, newHead, request.ExpectedOldHead, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? updateError = MapCommandFailure(operation, repository, updateResult, request.Branch);
        if (updateError is not null) return updateError;
        return prepared with
        {
            Operation = operation,
            CommitHash = newHead,
            HistoryRewrite = preview with { NewHead = newHead, RecoveryReference = recovery },
        };
    }

    /// <inheritdoc />
    public async Task<GitGatewayResult> ForcePushWithLeaseAsync(
        string repository, GitForceWithLeaseRequest request, CancellationToken cancellationToken = default)
    {
        const string operation = "force_push_with_lease";
        if (!IsRewriteInputValid(request.Branch, request.ExpectedLocalHead, request.Reason)
            || !FullShaPattern.IsMatch(request.ExpectedRemoteHead))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.InvalidReference, request.Branch);
        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken).ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null) return boundary.Failure!;
        RepositoryOptions options = boundary.Repository;
        if (!options.HistoryRewriteBranches.Contains(request.Branch))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.HistoryRewriteNotAllowed, request.Branch);
        GitGatewayResult? localFailure = await ValidateLocalRewriteStateAsync(
            operation, repository, options, request.Branch, request.ExpectedLocalHead, cancellationToken).ConfigureAwait(false);
        if (localFailure is not null) return localFailure;

        GitCommandResult remoteBefore = await _git.GetActualRemoteHeadAsync(
            options.LocalRoot, options.Remote, request.Branch, repository, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? remoteError = MapCommandFailure(operation, repository, remoteBefore, request.Branch);
        if (remoteError is not null) return remoteError;
        string actual = ParseRemoteHead(remoteBefore.StandardOutput);
        if (!string.Equals(actual, request.ExpectedRemoteHead, StringComparison.OrdinalIgnoreCase))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.ExpectedHeadMismatch, request.Branch);

        GitCommandResult push = await _git.ForcePushWithLeaseAsync(
            options.LocalRoot, options.Remote, request.Branch, request.ExpectedRemoteHead, repository, cancellationToken)
            .ConfigureAwait(false);
        GitGatewayResult? pushError = MapCommandFailure(operation, repository, push, request.Branch);
        if (pushError is not null) return pushError;
        GitCommandResult remoteAfter = await _git.GetActualRemoteHeadAsync(
            options.LocalRoot, options.Remote, request.Branch, repository, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? verifyError = MapCommandFailure(operation, repository, remoteAfter, request.Branch);
        if (verifyError is not null) return verifyError;
        string verified = ParseRemoteHead(remoteAfter.StandardOutput);
        if (!string.Equals(verified, request.ExpectedLocalHead, StringComparison.OrdinalIgnoreCase))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.RemoteVerificationFailed, request.Branch);
        return new(true, operation, repository, request.Branch, null, null,
            ForceWithLease: new(options.Remote, request.Branch, request.ExpectedRemoteHead,
                request.ExpectedLocalHead, verified, true));
    }

    private async Task<GitGatewayResult> PrepareHistoryRewriteAsync(
        string repository, GitHistoryRewriteRequest request, string operation, CancellationToken cancellationToken)
    {
        if (!IsRewriteInputValid(request.Branch, request.ExpectedOldHead, request.Reason))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.InvalidReference, request.Branch);
        BoundaryResult boundary = await ValidateBoundaryAsync(repository, operation, cancellationToken).ConfigureAwait(false);
        if (!boundary.IsValid || boundary.Repository is null) return boundary.Failure!;
        RepositoryOptions options = boundary.Repository;
        if (!options.HistoryRewriteBranches.Contains(request.Branch))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.HistoryRewriteNotAllowed, request.Branch);
        GitGatewayResult? localFailure = await ValidateLocalRewriteStateAsync(
            operation, repository, options, request.Branch, request.ExpectedOldHead, cancellationToken).ConfigureAwait(false);
        if (localFailure is not null) return localFailure;
        GitCommandResult metadata = await _git.GetCommitMetadataAsync(
            options.LocalRoot, request.ExpectedOldHead, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? metadataError = MapCommandFailure(operation, repository, metadata, request.Branch);
        if (metadataError is not null) return metadataError;
        string[] f = metadata.StandardOutput.Split('\u001f', 11);
        if (f.Length != 11) return GitGatewayResult.Failure(operation, repository, GitGatewayError.GitFailed, request.Branch);
        GitIdentity authorBefore = new(f[3], f[4]);
        GitIdentity committerBefore = new(f[6], f[7]);
        GitIdentity authorAfter = new(request.AuthorName ?? f[3], request.AuthorEmail ?? f[4]);
        GitIdentity committerAfter = new(request.CommitterName ?? f[6], request.CommitterEmail ?? f[7]);
        if (!IsIdentityValid(authorAfter) || !IsIdentityValid(committerAfter))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.InvalidIdentity, request.Branch);
        if (authorAfter == authorBefore && committerAfter == committerBefore)
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.NoIdentityChange, request.Branch);
        bool signed = !string.Equals(f[9], "N", StringComparison.Ordinal);
        if (signed && !request.AllowSignatureRemoval)
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.SignedCommitConfirmationRequired, request.Branch);
        GitCommandResult remote = await _git.GetActualRemoteHeadAsync(
            options.LocalRoot, options.Remote, request.Branch, repository, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? remoteError = MapCommandFailure(operation, repository, remote, request.Branch);
        if (remoteError is not null) return remoteError;
        bool remoteUpdateRequired = string.Equals(ParseRemoteHead(remote.StandardOutput), request.ExpectedOldHead,
            StringComparison.OrdinalIgnoreCase);
        GitHistoryRewriteData data = new(options.Remote, request.Branch, request.ExpectedOldHead, null,
            authorBefore, authorAfter, committerBefore, committerAfter, f[5], f[8], true,
            signed, signed, remoteUpdateRequired, null, false);
        return new(true, operation, repository, request.Branch, null, null, HistoryRewrite: data);
    }

    private async Task<GitGatewayResult?> ValidateLocalRewriteStateAsync(
        string operation, string repository, RepositoryOptions options, string branch, string expectedHead,
        CancellationToken cancellationToken)
    {
        GitCommandResult branchResult = await _git.GetCurrentBranchAsync(options.LocalRoot, cancellationToken).ConfigureAwait(false);
        GitGatewayResult? error = MapCommandFailure(operation, repository, branchResult, branch);
        if (error is not null) return error;
        if (!string.Equals(branchResult.StandardOutput.Trim(), branch, StringComparison.Ordinal))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.BranchNotCheckedOut, branch);
        GitCommandResult head = await _git.GetHeadAsync(options.LocalRoot, cancellationToken).ConfigureAwait(false);
        error = MapCommandFailure(operation, repository, head, branch);
        if (error is not null) return error;
        if (!string.Equals(head.StandardOutput.Trim(), expectedHead, StringComparison.OrdinalIgnoreCase))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.ExpectedHeadMismatch, branch);
        GitCommandResult status = await _git.GetStatusAsync(options.LocalRoot, cancellationToken).ConfigureAwait(false);
        error = MapCommandFailure(operation, repository, status, branch);
        if (error is not null) return error;
        if (!string.IsNullOrWhiteSpace(status.StandardOutput))
            return GitGatewayResult.Failure(operation, repository, GitGatewayError.WorkingTreeDirty, branch);
        GitCommandResult unfinished = await _git.GetUnfinishedOperationAsync(options.LocalRoot, cancellationToken).ConfigureAwait(false);
        error = MapCommandFailure(operation, repository, unfinished, branch);
        if (error is not null) return error;
        return string.IsNullOrWhiteSpace(unfinished.StandardOutput) ? null
            : GitGatewayResult.Failure(operation, repository, GitGatewayError.UnfinishedOperation, branch);
    }

    private static bool IsRewriteInputValid(string branch, string head, string reason) =>
        BranchPattern.IsMatch(branch) && FullShaPattern.IsMatch(head)
        && !string.IsNullOrWhiteSpace(reason) && reason.Length <= 1024;

    private static bool IsIdentityValid(GitIdentity identity) =>
        !string.IsNullOrWhiteSpace(identity.Name) && identity.Name.Length <= 256
        && !identity.Name.Any(char.IsControl) && !string.IsNullOrWhiteSpace(identity.Email)
        && identity.Email.Length <= 320 && identity.Email.Contains('@', StringComparison.Ordinal)
        && !identity.Email.Any(char.IsControl);

    private static string ParseRemoteHead(string output) =>
        output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    private async Task<BoundaryResult> ValidateBoundaryAsync(
        string repository,
        string operation,
        CancellationToken cancellationToken)
    {
        if (!RepositoryId.IsLookupValid(repository))
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
        if (remoteValidation.IsValid) return BoundaryResult.Valid(options);
        GitGatewayError error = remoteValidation.Error == RepositoryValidationError.SshRemoteNotSupported
            ? GitGatewayError.SshRemoteNotSupported
            : GitGatewayError.RemoteMismatch;
        return BoundaryResult.Invalid(GitGatewayResult.Failure(operation, repository, error));
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
            _ when ContainsAny(result.StandardError, "cannot lock ref", "stale info") =>
                GitGatewayError.ExpectedHeadMismatch,
            _ when ContainsAny(result.StandardError,
                "authentication failed", "could not read Username", "terminal prompts disabled",
                "invalid credentials", "Authentication failed") => GitGatewayError.AuthenticationFailed,
            _ when ContainsAny(result.StandardError,
                "Could not resolve host", "Failed to connect", "Connection timed out",
                "Connection reset", "Network is unreachable") => GitGatewayError.NetworkError,
            _ when ContainsAny(result.StandardError,
                "Permission denied", "403 Forbidden", "access denied", "not permitted") =>
                GitGatewayError.PermissionDenied,
            _ when ContainsAny(result.StandardError,
                "would be overwritten by merge", "Your local changes", "unstaged changes") =>
                GitGatewayError.WorkingTreeDirty,
            _ when ContainsAny(result.StandardError,
                "CONFLICT (", "Automatic merge failed", "Merge conflict") => GitGatewayError.Conflict,
            _ when ContainsAny(result.StandardError,
                "src refspec", "does not match any", "unknown revision") => GitGatewayError.ReferenceNotFound,
            _ when ContainsAny(result.StandardError,
                "does not appear to be a git repository", "No such remote", "remote origin already exists") =>
                GitGatewayError.RemoteMismatch,
            _ => GitGatewayError.GitFailed,
        };
        return GitGatewayResult.DiagnosticFailure(
            operation,
            repository,
            error,
            branch,
            SanitizeErrorDetail(result.StandardError));
    }

    private static string SanitizeErrorDetail(string standardError)
    {
        if (string.IsNullOrWhiteSpace(standardError))
        {
            return "Git did not provide diagnostic details.";
        }

        string detail = SecretPattern.Replace(standardError, "$1=(redacted)");
        detail = UrlPattern.Replace(detail, "(redacted-url)");
        detail = EmailPattern.Replace(detail, "(redacted-email)");
        detail = WindowsPathPattern.Replace(detail, "(redacted-path)");
        detail = UnixPathPattern.Replace(detail, "(redacted-path)");
        detail = WhitespacePattern.Replace(detail, " ").Trim();
        return detail.Length <= MaximumErrorDetailLength
            ? detail
            : string.Concat(detail.AsSpan(0, MaximumErrorDetailLength), "…");
    }

    private static bool ContainsAny(string value, params string[] patterns) =>
        patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));

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

    private static bool TryParseAheadBehind(string value, out int ahead, out int behind)
    {
        ahead = 0;
        behind = 0;
        string[] counts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return counts.Length == 2
            && int.TryParse(counts[0], NumberStyles.None, CultureInfo.InvariantCulture, out ahead)
            && int.TryParse(counts[1], NumberStyles.None, CultureInfo.InvariantCulture, out behind)
            && ahead >= 0
            && behind >= 0;
    }

    private sealed record BoundaryResult(
        bool IsValid,
        RepositoryOptions? Repository,
        GitGatewayResult? Failure)
    {
        internal static BoundaryResult Valid(RepositoryOptions repository) => new(true, repository, null);

        internal static BoundaryResult Invalid(GitGatewayResult failure) => new(false, null, failure);
    }
}
