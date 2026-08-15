namespace Buckettie.Application.Configuration;

/// <summary>
/// Buckettieの構成設定を表します。
/// </summary>
public sealed record BuckettieOptions
{
    /// <summary>
    /// Repository IDをキーとする許可済みRepository設定です。
    /// </summary>
    public required IReadOnlyDictionary<string, RepositoryOptions> Repositories { get; init; }
}

/// <summary>
/// 許可済みRepositoryの設定を表します。
/// </summary>
public sealed record RepositoryOptions
{
    /// <summary>Bitbucket Workspace名です。</summary>
    public required string Workspace { get; init; }

    /// <summary>Bitbucket Repository Slugです。</summary>
    public required string Slug { get; init; }

    /// <summary>ローカルRepositoryのルートです。</summary>
    public required string LocalRoot { get; init; }

    /// <summary>検証対象のGit Remote名です。</summary>
    public required string Remote { get; init; }

    /// <summary>開発ブランチ名です。</summary>
    public required string DevelopBranch { get; init; }

    /// <summary>本番ブランチ名です。</summary>
    public required string MainBranch { get; init; }

    /// <summary>直接Pushを許可するブランチです。</summary>
    public required HashSet<string> DirectPushBranches { get; init; }

    /// <summary>Pullを許可するブランチです。</summary>
    public required HashSet<string> PullBranches { get; init; }

    /// <summary>保護対象のブランチです。</summary>
    public required HashSet<string> ProtectedBranches { get; init; }

    /// <summary>Release Tagの対象ブランチです。</summary>
    public required string TagTargetBranch { get; init; }

    /// <summary>許可するTag名の正規表現です。</summary>
    public required string TagPattern { get; init; }

    /// <summary>Push時にcleanな作業ツリーを要求するかを示します。</summary>
    public bool RequireCleanWorkingTree { get; init; } = true;
}
