using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;

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
        if (string.IsNullOrWhiteSpace(branch) || branch.Length > 255 || branch.Any(char.IsControl))
        {
            return Task.FromResult(BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.InvalidBranch));
        }

        return TryGet(repository, out RepositoryOptions? options)
            ? _client.GetBranchAsync(repository, options!.Workspace, options.Slug, branch, cancellationToken)
            : Task.FromResult(BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.RepositoryNotAllowed));
    }

    /// <inheritdoc />
    public Task<BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(
        string repository,
        BitbucketPullRequestState? state,
        CancellationToken cancellationToken = default)
    {
        if (state is not null && !Enum.IsDefined(state.Value))
        {
            return Task.FromResult(BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Failure(
                BitbucketError.InvalidPullRequest));
        }

        return TryGet(repository, out RepositoryOptions? options)
            ? _client.ListPullRequestsAsync(repository, options!.Workspace, options.Slug, state, cancellationToken)
            : Task.FromResult(BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Failure(
                BitbucketError.RepositoryNotAllowed));
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
        return RepositoryId.IsValid(repository) && _allowlist.TryGet(repository, out options);
    }
}
