using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using Buckettie.Domain;

namespace Buckettie.Application.Bitbucket;

/// <summary>Repository Allowlistを適用するBitbucket REST Gatewayです。</summary>
public sealed class BitbucketRepositoryGateway : IBitbucketRepositoryGateway
{
    private readonly RepositoryAllowlist _allowlist;
    private readonly IBitbucketApiClient _client;

    /// <summary>Gatewayを初期化します。</summary>
    public BitbucketRepositoryGateway(RepositoryAllowlist allowlist, IBitbucketApiClient client)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(client);
        _allowlist = allowlist;
        _client = client;
    }

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketRepositoryInfo>> GetRepositoryAsync(
        string repository,
        CancellationToken cancellationToken = default) =>
        TryGet(repository, out RepositoryOptions? options)
            ? _client.GetRepositoryAsync(repository, options!.Workspace, options.Slug, cancellationToken)
            : Task.FromResult(BitbucketResult<BitbucketRepositoryInfo>.Failure(BitbucketError.RepositoryNotAllowed));

    /// <inheritdoc />
    public Task<BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>> ListBranchesAsync(
        string repository,
        CancellationToken cancellationToken = default) =>
        TryGet(repository, out RepositoryOptions? options)
            ? _client.ListBranchesAsync(repository, options!.Workspace, options.Slug, cancellationToken)
            : Task.FromResult(BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>.Failure(
                BitbucketError.RepositoryNotAllowed));

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketBranchInfo>> GetBranchAsync(
        string repository,
        string branch,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidBranchInput(branch))
        {
            return Task.FromResult(BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.InvalidBranch));
        }

        return TryGet(repository, out RepositoryOptions? options)
            ? _client.GetBranchAsync(repository, options!.Workspace, options.Slug, branch, cancellationToken)
            : Task.FromResult(BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.RepositoryNotAllowed));
    }

    /// <inheritdoc />
    public async Task<BitbucketResult<BitbucketBranchInfo>> CreateBranchAsync(
        string repository,
        string branch,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidBranchInput(branch))
        {
            return BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.InvalidBranch);
        }

        if (!BranchSource.IsValid(source))
        {
            return BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.InvalidBranchSource);
        }

        if (!TryGet(repository, out RepositoryOptions? options) || options is null)
        {
            return BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.RepositoryNotAllowed);
        }

        BitbucketResult<string> resolved = await ResolveSourceAsync(
            repository, options, source, cancellationToken).ConfigureAwait(false);
        if (!resolved.IsSuccess || !BranchSource.IsCommit(resolved.Value))
        {
            return BitbucketResult<BitbucketBranchInfo>.Failure(resolved.Error ?? BitbucketError.InvalidResponse);
        }

        BitbucketResult<BitbucketBranchInfo> created = await _client.CreateBranchAsync(
            repository,
            options.Workspace,
            options.Slug,
            new BitbucketBranchCreate(branch, resolved.Value!),
            cancellationToken).ConfigureAwait(false);
        if (!created.IsSuccess || created.Value is null)
        {
            return created;
        }

        if (!string.Equals(created.Value.Name, branch, StringComparison.Ordinal)
            || !string.Equals(created.Value.TargetHash, resolved.Value, StringComparison.OrdinalIgnoreCase))
        {
            return BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.InvalidResponse);
        }

        return BitbucketResult<BitbucketBranchInfo>.Success(created.Value with
        {
            Source = source,
            SourceKind = BranchSource.IsCommit(source) ? "commit" : "branch",
            SourceHash = resolved.Value,
        });
    }

    private async Task<BitbucketResult<string>> ResolveSourceAsync(
        string repository, RepositoryOptions options, string source, CancellationToken cancellationToken)
    {
        if (BranchSource.IsCommit(source))
        {
            BitbucketResult<string> commit = await _client.GetCommitAsync(
                repository, options.Workspace, options.Slug, source, cancellationToken).ConfigureAwait(false);
            return commit.Error == BitbucketError.NotFound
                ? BitbucketResult<string>.Failure(BitbucketError.SourceCommitNotFound)
                : commit;
        }

        BitbucketResult<BitbucketBranchInfo> branch = await _client.GetBranchAsync(
            repository, options.Workspace, options.Slug, source, cancellationToken).ConfigureAwait(false);
        return branch.IsSuccess && branch.Value is not null
            ? BitbucketResult<string>.Success(branch.Value.TargetHash)
            : BitbucketResult<string>.Failure(branch.Error == BitbucketError.NotFound
                ? BitbucketError.SourceBranchNotFound : branch.Error ?? BitbucketError.InvalidResponse);
    }

    /// <inheritdoc />
    public Task<BitbucketResult<bool>> DeleteBranchAsync(
        string repository,
        string branch,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidBranchInput(branch))
        {
            return Task.FromResult(BitbucketResult<bool>.Failure(BitbucketError.InvalidBranch));
        }

        if (!TryGet(repository, out RepositoryOptions? options) || options is null)
        {
            return Task.FromResult(BitbucketResult<bool>.Failure(BitbucketError.RepositoryNotAllowed));
        }

        if (string.Equals(branch, options.DevelopBranch, StringComparison.Ordinal)
            || string.Equals(branch, options.MainBranch, StringComparison.Ordinal)
            || options.ProtectedBranches.Contains(branch))
        {
            return Task.FromResult(BitbucketResult<bool>.Failure(BitbucketError.BranchProtected));
        }

        return _client.DeleteBranchAsync(
            repository, options.Workspace, options.Slug, branch, cancellationToken);
    }

    /// <inheritdoc />
    public Task<BitbucketResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(
        string repository,
        CancellationToken cancellationToken = default) =>
        TryGet(repository, out RepositoryOptions? options)
            ? _client.ListTagsAsync(repository, options!.Workspace, options.Slug, cancellationToken)
            : Task.FromResult(BitbucketResult<IReadOnlyList<BitbucketTagInfo>>.Failure(
                BitbucketError.RepositoryNotAllowed));

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketTagInfo>> GetTagAsync(
        string repository,
        string tag,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidTagInput(tag))
        {
            return Task.FromResult(BitbucketResult<BitbucketTagInfo>.Failure(BitbucketError.InvalidTag));
        }

        return TryGet(repository, out RepositoryOptions? options)
            ? _client.GetTagAsync(repository, options!.Workspace, options.Slug, tag, cancellationToken)
            : Task.FromResult(BitbucketResult<BitbucketTagInfo>.Failure(BitbucketError.RepositoryNotAllowed));
    }

    /// <inheritdoc />
    public async Task<BitbucketResult<BitbucketTagInfo>> CreateTagAsync(
        string repository,
        BitbucketTagCreate input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IsValidTagInput(input.Name)
            || input.Message is { Length: > 32_768 }
            || input.Message?.Contains('\0', StringComparison.Ordinal) == true)
        {
            return BitbucketResult<BitbucketTagInfo>.Failure(BitbucketError.InvalidTag);
        }

        if (!TryGet(repository, out RepositoryOptions? options) || options is null)
        {
            return BitbucketResult<BitbucketTagInfo>.Failure(BitbucketError.RepositoryNotAllowed);
        }

        RepositoryPolicy policy = CreatePolicy(repository, options);
        PolicyResult policyResult = policy.ValidateTag(input.Name, options.TagTargetBranch);
        if (!policyResult.IsAllowed)
        {
            return BitbucketResult<BitbucketTagInfo>.Failure(
                policyResult.ErrorCode == PolicyErrorCode.TagTargetNotAllowed
                    ? BitbucketError.TagTargetNotAllowed
                    : BitbucketError.InvalidTag);
        }

        BitbucketResult<BitbucketBranchInfo> branch = await _client.GetBranchAsync(
            repository,
            options.Workspace,
            options.Slug,
            options.TagTargetBranch,
            cancellationToken).ConfigureAwait(false);
        if (!branch.IsSuccess || branch.Value is null)
        {
            return BitbucketResult<BitbucketTagInfo>.Failure(branch.Error ?? BitbucketError.InvalidResponse);
        }

        return await _client.CreateTagAsync(
            repository,
            options.Workspace,
            options.Slug,
            branch.Value.TargetHash,
            input,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<BitbucketResult<bool>> DeleteTagAsync(
        string repository,
        string tag,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidTagInput(tag))
        {
            return Task.FromResult(BitbucketResult<bool>.Failure(BitbucketError.InvalidTag));
        }

        if (!TryGet(repository, out RepositoryOptions? options) || options is null)
        {
            return Task.FromResult(BitbucketResult<bool>.Failure(BitbucketError.RepositoryNotAllowed));
        }

        RepositoryPolicy policy = CreatePolicy(repository, options);
        PolicyResult policyResult = policy.ValidateTag(tag, options.TagTargetBranch);
        if (!policyResult.IsAllowed)
        {
            return Task.FromResult(BitbucketResult<bool>.Failure(BitbucketError.InvalidTag));
        }

        return _client.DeleteTagAsync(repository, options.Workspace, options.Slug, tag, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(
        string repository,
        BitbucketPullRequestState? state,
        string? source,
        string? destination,
        CancellationToken cancellationToken = default)
    {
        if (state is not null && !Enum.IsDefined(state.Value))
        {
            return BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Failure(
                BitbucketError.InvalidPullRequest);
        }

        if (!IsValidOptionalBranch(source) || !IsValidOptionalBranch(destination))
        {
            return BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Failure(BitbucketError.InvalidBranch);
        }

        if (!TryGet(repository, out RepositoryOptions? options) || options is null)
        {
            return BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Failure(
                BitbucketError.RepositoryNotAllowed);
        }

        BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>> result = await _client.ListPullRequestsAsync(
            repository,
            options.Workspace,
            options.Slug,
            state,
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return result;
        }

        IReadOnlyList<BitbucketPullRequestInfo> filtered = result.Value
            .Where(pullRequest => source is null
                || string.Equals(pullRequest.SourceBranch, source, StringComparison.Ordinal))
            .Where(pullRequest => destination is null
                || string.Equals(pullRequest.DestinationBranch, destination, StringComparison.Ordinal))
            .ToArray();
        return BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Success(filtered);
    }

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketPullRequestInfo>> GetPullRequestAsync(
        string repository,
        int pullRequestId,
        CancellationToken cancellationToken = default) =>
        TryGetPullRequest(repository, pullRequestId, out RepositoryOptions? options)
            ? _client.GetPullRequestAsync(
                repository,
                options!.Workspace,
                options.Slug,
                pullRequestId,
                cancellationToken)
            : Task.FromResult(BitbucketResult<BitbucketPullRequestInfo>.Failure(
                pullRequestId > 0 ? BitbucketError.RepositoryNotAllowed : BitbucketError.InvalidPullRequest));

    /// <inheritdoc />
    public Task<BitbucketResult<string>> GetPullRequestDiffAsync(
        string repository,
        int pullRequestId,
        CancellationToken cancellationToken = default) =>
        TryGetPullRequest(repository, pullRequestId, out RepositoryOptions? options)
            ? _client.GetPullRequestDiffAsync(
                repository,
                options!.Workspace,
                options.Slug,
                pullRequestId,
                cancellationToken)
            : Task.FromResult(BitbucketResult<string>.Failure(
                pullRequestId > 0 ? BitbucketError.RepositoryNotAllowed : BitbucketError.InvalidPullRequest));

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketPullRequestInfo>> CreatePullRequestAsync(
        string repository,
        BitbucketPullRequestCreate input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 255 || input.Title.Any(char.IsControl)
            || input.Description is null
            || input.Description.Length > 32_768
            || input.Description.Any(character => character == '\0'))
        {
            return Task.FromResult(BitbucketResult<BitbucketPullRequestInfo>.Failure(
                BitbucketError.InvalidPullRequest));
        }

        return TryGet(repository, out RepositoryOptions? options)
            ? _client.CreatePullRequestAsync(
                repository,
                options!.Workspace,
                options.Slug,
                options.DevelopBranch,
                options.MainBranch,
                input,
                cancellationToken)
            : Task.FromResult(BitbucketResult<BitbucketPullRequestInfo>.Failure(
                BitbucketError.RepositoryNotAllowed));
    }

    /// <inheritdoc />
    public async Task<BitbucketResult<BitbucketPullRequestInfo>> MergePullRequestAsync(
        string repository,
        int pullRequestId,
        BitbucketPullRequestMerge input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.Strategy))
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.InvalidPullRequest);
        }

        if (!TryGetPullRequest(repository, pullRequestId, out RepositoryOptions? options) || options is null)
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(
                pullRequestId > 0 ? BitbucketError.RepositoryNotAllowed : BitbucketError.InvalidPullRequest);
        }

        if (input.Message is { Length: > 32_768 } || input.Message?.Contains('\0', StringComparison.Ordinal) == true)
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.InvalidPullRequest);
        }

        BitbucketResult<BitbucketPullRequestInfo> current = await _client.GetPullRequestAsync(
            repository,
            options.Workspace,
            options.Slug,
            pullRequestId,
            cancellationToken).ConfigureAwait(false);
        if (!current.IsSuccess || current.Value is null)
        {
            return current;
        }

        if (!string.Equals(current.Value.State, "OPEN", StringComparison.Ordinal))
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.PullRequestNotOpen);
        }

        if (!string.Equals(current.Value.SourceBranch, options.DevelopBranch, StringComparison.Ordinal)
            || !string.Equals(current.Value.DestinationBranch, options.MainBranch, StringComparison.Ordinal))
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.PullRequestRouteNotAllowed);
        }

        return await _client.MergePullRequestAsync(
            repository,
            options.Workspace,
            options.Slug,
            pullRequestId,
            input,
            cancellationToken).ConfigureAwait(false);
    }

    private bool TryGetPullRequest(string repository, int pullRequestId, out RepositoryOptions? options)
    {
        options = null;
        return pullRequestId > 0 && TryGet(repository, out options);
    }

    private bool TryGet(string repository, out RepositoryOptions? options)
    {
        options = null;
        return RepositoryId.IsLookupValid(repository) && _allowlist.TryGet(repository, out options);
    }

    private static bool IsValidTagInput(string tag) =>
        !string.IsNullOrWhiteSpace(tag) && tag.Length <= 255 && !tag.Any(char.IsControl);

    private static bool IsValidBranchInput(string branch) =>
        !string.IsNullOrWhiteSpace(branch) && branch.Length <= 255 && !branch.Any(char.IsControl);

    private static bool IsValidOptionalBranch(string? branch) =>
        branch is null || (!string.IsNullOrWhiteSpace(branch) && branch.Length <= 255 && !branch.Any(char.IsControl));

    private static RepositoryPolicy CreatePolicy(string repository, RepositoryOptions options) => new(
        repository,
        options.DevelopBranch,
        options.MainBranch,
        options.DirectPushBranches,
        options.PullBranches,
        new HashSet<PullRequestRoute> { new(options.DevelopBranch, options.MainBranch) },
        options.ProtectedBranches,
        options.TagTargetBranch,
        options.TagPattern,
        options.RequireCleanWorkingTree);
}
