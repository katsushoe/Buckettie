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

    private bool TryGet(string repository, out RepositoryOptions? options)
    {
        options = null;
        return RepositoryId.IsValid(repository) && _allowlist.TryGet(repository, out options);
    }
}
