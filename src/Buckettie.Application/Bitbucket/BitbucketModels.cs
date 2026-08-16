namespace Buckettie.Application.Bitbucket;

/// <summary>Bitbucket Repository情報です。</summary>
public sealed record BitbucketRepositoryInfo(
    string Uuid,
    string FullName,
    string Name,
    bool IsPrivate,
    string? MainBranch);

/// <summary>Bitbucket Branch情報です。</summary>
public sealed record BitbucketBranchInfo(string Name, string TargetHash);

/// <summary>Bitbucket Tag情報です。</summary>
public sealed record BitbucketTagInfo(
    string Name,
    string TargetHash,
    string? Message,
    DateTimeOffset? Date,
    string? Tagger);

/// <summary>Tag作成入力です。</summary>
public sealed record BitbucketTagCreate(string Name, string? Message);

/// <summary>Pull Requestの状態です。</summary>
public enum BitbucketPullRequestState
{
    Open,
    Merged,
    Declined,
    Superseded,
}

/// <summary>Pull Request情報です。</summary>
public sealed record BitbucketPullRequestInfo(
    int Id,
    string Title,
    string Description,
    string State,
    string SourceBranch,
    string DestinationBranch,
    bool Draft,
    string? Url,
    DateTimeOffset CreatedOn,
    DateTimeOffset UpdatedOn,
    string? MergeCommitHash);

/// <summary>Pull Request作成入力です。</summary>
public sealed record BitbucketPullRequestCreate(string Title, string Description, bool Draft);

/// <summary>Pull Request merge strategyです。</summary>
public enum BitbucketMergeStrategy
{
    RepositoryDefault,
    MergeCommit,
    Squash,
    FastForward,
}

/// <summary>Pull Request merge入力です。</summary>
public sealed record BitbucketPullRequestMerge(BitbucketMergeStrategy Strategy, string? Message);
