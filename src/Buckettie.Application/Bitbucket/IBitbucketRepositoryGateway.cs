namespace Buckettie.Application.Bitbucket;

/// <summary>Allowlistを適用するBitbucket Repository読取Gatewayです。</summary>
public interface IBitbucketRepositoryGateway
{
    /// <summary>Repository情報を取得します。</summary>
    public Task<BitbucketResult<BitbucketRepositoryInfo>> GetRepositoryAsync(
        string repository,
        CancellationToken cancellationToken = default);

    /// <summary>Branch一覧を取得します。</summary>
    public Task<BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>> ListBranchesAsync(
        string repository,
        CancellationToken cancellationToken = default);

    /// <summary>Branch詳細を取得します。</summary>
    public Task<BitbucketResult<BitbucketBranchInfo>> GetBranchAsync(
        string repository,
        string branch,
        CancellationToken cancellationToken = default);
}
