namespace Buckettie.Application.Bitbucket;

/// <summary>固定Bitbucket REST操作のInfrastructure境界です。</summary>
public interface IBitbucketApiClient
{
    /// <summary>Repository情報を取得します。</summary>
    public Task<BitbucketResult<BitbucketRepositoryInfo>> GetRepositoryAsync(
        string repositoryId,
        string workspace,
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>Branch一覧を取得します。</summary>
    public Task<BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>> ListBranchesAsync(
        string repositoryId,
        string workspace,
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>Branch詳細を取得します。</summary>
    public Task<BitbucketResult<BitbucketBranchInfo>> GetBranchAsync(
        string repositoryId,
        string workspace,
        string slug,
        string branch,
        CancellationToken cancellationToken = default);
}
