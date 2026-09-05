namespace Buckettie.Application.Configuration;

/// <summary>
/// Buckettieの構成設定を表します。
/// </summary>
public sealed record BuckettieOptions
{
    /// <summary>UI表示言語です。auto、ja-JP、en-USのいずれかを指定します。</summary>
    public string Language { get; init; } = "auto";

    /// <summary>MCP ServerがlocalhostでListenするTCP portです。</summary>
    public int McpPort { get; init; } = 45450;

    /// <summary>MCP Endpoint pathです。</summary>
    public string McpPath { get; init; } = "/mcp";

    /// <summary>
    /// Bitbucket API Tokenと組み合わせるAtlassian emailです。
    /// </summary>
    public required string AtlassianEmail { get; init; }

    /// <summary>
    /// Git HTTPS認証に使用するBitbucket Cloud usernameです。
    /// </summary>
    public required string BitbucketUsername { get; init; }

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

    /// <summary>履歴書き換え操作を明示的に許可するブランチです。</summary>
    public HashSet<string> HistoryRewriteBranches { get; init; } = [];
}
