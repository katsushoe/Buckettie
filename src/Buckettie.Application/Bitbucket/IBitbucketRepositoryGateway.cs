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

    /// <summary>Tag一覧を取得します。</summary>
    public Task<BitbucketResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(
        string repository,
        CancellationToken cancellationToken = default);

    /// <summary>Tag詳細を取得します。</summary>
    public Task<BitbucketResult<BitbucketTagInfo>> GetTagAsync(
        string repository,
        string tag,
        CancellationToken cancellationToken = default);

    /// <summary>設定済み対象BranchのHEADへTagを作成します。</summary>
    public Task<BitbucketResult<BitbucketTagInfo>> CreateTagAsync(
        string repository,
        BitbucketTagCreate input,
        CancellationToken cancellationToken = default);

    /// <summary>Pull Request一覧を取得します。</summary>
    public Task<BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(
        string repository,
        BitbucketPullRequestState? state,
        string? source,
        string? destination,
        CancellationToken cancellationToken = default);

    /// <summary>Pull Request詳細を取得します。</summary>
    public Task<BitbucketResult<BitbucketPullRequestInfo>> GetPullRequestAsync(
        string repository,
        int pullRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>Pull Request diffを取得します。</summary>
    public Task<BitbucketResult<string>> GetPullRequestDiffAsync(
        string repository,
        int pullRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>設定済み経路でPull Requestを作成します。</summary>
    public Task<BitbucketResult<BitbucketPullRequestInfo>> CreatePullRequestAsync(
        string repository,
        BitbucketPullRequestCreate input,
        CancellationToken cancellationToken = default);

    /// <summary>Policy検証後にPull Requestをmergeします。</summary>
    public Task<BitbucketResult<BitbucketPullRequestInfo>> MergePullRequestAsync(
        string repository,
        int pullRequestId,
        BitbucketPullRequestMerge input,
        CancellationToken cancellationToken = default);
}
