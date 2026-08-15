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
