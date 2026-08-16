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

    /// <summary>Tag一覧を取得します。</summary>
    public Task<BitbucketResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(
        string repositoryId,
        string workspace,
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>Tag詳細を取得します。</summary>
    public Task<BitbucketResult<BitbucketTagInfo>> GetTagAsync(
        string repositoryId,
        string workspace,
        string slug,
        string tag,
        CancellationToken cancellationToken = default);

    /// <summary>指定CommitへTagを作成します。</summary>
    public Task<BitbucketResult<BitbucketTagInfo>> CreateTagAsync(
        string repositoryId,
        string workspace,
        string slug,
        string targetHash,
        BitbucketTagCreate input,
        CancellationToken cancellationToken = default);

    /// <summary>Pull Request一覧を取得します。</summary>
    public Task<BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(
        string repositoryId,
        string workspace,
        string slug,
        BitbucketPullRequestState? state,
        CancellationToken cancellationToken = default);

    /// <summary>Pull Request詳細を取得します。</summary>
    public Task<BitbucketResult<BitbucketPullRequestInfo>> GetPullRequestAsync(
        string repositoryId,
        string workspace,
        string slug,
        int pullRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>Pull Request diffを取得します。</summary>
    public Task<BitbucketResult<string>> GetPullRequestDiffAsync(
        string repositoryId,
        string workspace,
        string slug,
        int pullRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>Pull Requestを作成します。</summary>
    public Task<BitbucketResult<BitbucketPullRequestInfo>> CreatePullRequestAsync(
        string repositoryId,
        string workspace,
        string slug,
        string sourceBranch,
        string destinationBranch,
        BitbucketPullRequestCreate input,
        CancellationToken cancellationToken = default);

    /// <summary>Pull Requestをmergeします。</summary>
    public Task<BitbucketResult<BitbucketPullRequestInfo>> MergePullRequestAsync(
        string repositoryId,
        string workspace,
        string slug,
        int pullRequestId,
        BitbucketPullRequestMerge input,
        CancellationToken cancellationToken = default);
}
