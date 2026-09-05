namespace Buckettie.Application.Git;

/// <summary>
/// Buckettieが許可する固定Gitコマンドだけを公開します。
/// </summary>
public interface IGitCommandClient
{
    /// <summary>現在のbranch名を取得します。</summary>
    public Task<GitCommandResult> GetCurrentBranchAsync(string repositoryRoot, CancellationToken cancellationToken);

    /// <summary>現在のHEAD hashを取得します。</summary>
    public Task<GitCommandResult> GetHeadAsync(string repositoryRoot, CancellationToken cancellationToken);

    /// <summary>設定済みRemote追跡BranchのHEAD hashを取得します。</summary>
    public Task<GitCommandResult> GetRemoteHeadAsync(
        string repositoryRoot,
        string remote,
        string branch,
        CancellationToken cancellationToken);

    /// <summary>local HEADのRemote追跡Branchに対するahead/behind件数を取得します。</summary>
    public Task<GitCommandResult> GetAheadBehindAsync(
        string repositoryRoot,
        string remote,
        string branch,
        CancellationToken cancellationToken);

    /// <summary>作業ツリー状態を取得します。</summary>
    public Task<GitCommandResult> GetStatusAsync(string repositoryRoot, CancellationToken cancellationToken);

    /// <summary>HEADに対する作業ツリー差分を取得します。</summary>
    public Task<GitCommandResult> GetDiffAsync(string repositoryRoot, CancellationToken cancellationToken);

    /// <summary>作業ツリーの変更をすべてIndexへ追加します。</summary>
    public Task<GitCommandResult> StageAllAsync(string repositoryRoot, CancellationToken cancellationToken);

    /// <summary>Indexの変更を指定メッセージでcommitします。</summary>
    public Task<GitCommandResult> CommitAsync(
        string repositoryRoot, string message, CancellationToken cancellationToken);

    /// <summary>設定済みRemote URLを取得します。</summary>
    public Task<GitCommandResult> GetRemoteUrlAsync(
        string repositoryRoot,
        string remote,
        CancellationToken cancellationToken);

    /// <summary>設定済みRemoteからfetchします。</summary>
    public Task<GitCommandResult> FetchAsync(
        string repositoryRoot,
        string remote,
        string repositoryId,
        CancellationToken cancellationToken);

    /// <summary>設定済みRemoteからfast-forward限定でpullします。</summary>
    public Task<GitCommandResult> PullFastForwardOnlyAsync(
        string repositoryRoot,
        string remote,
        string branch,
        string repositoryId,
        CancellationToken cancellationToken);

    /// <summary>現在branchを設定済みRemoteへpushします。</summary>
    public Task<GitCommandResult> PushAsync(
        string repositoryRoot,
        string remote,
        string branch,
        string repositoryId,
        CancellationToken cancellationToken);

    /// <summary>指定Tagを設定済みRemoteへpushします。</summary>
    public Task<GitCommandResult> PushTagAsync(
        string repositoryRoot, string remote, string tag, string repositoryId,
        CancellationToken cancellationToken);

    /// <summary>commit-treeで再構築するための最新commit情報を取得します。</summary>
    public Task<GitCommandResult> GetCommitMetadataAsync(string repositoryRoot, string commit, CancellationToken cancellationToken);

    /// <summary>未完了のmerge/rebase等があるかを取得します。</summary>
    public Task<GitCommandResult> GetUnfinishedOperationAsync(string repositoryRoot, CancellationToken cancellationToken);

    /// <summary>復旧用refを作成します。</summary>
    public Task<GitCommandResult> CreateReferenceAsync(string repositoryRoot, string reference, string target, CancellationToken cancellationToken);

    /// <summary>元commitと同一tree/parents/messageでidentityだけを変更したcommitを作成します。</summary>
    public Task<GitCommandResult> CreateCommitAsync(string repositoryRoot, string metadata, IReadOnlyDictionary<string, string> identityEnvironment, CancellationToken cancellationToken);

    /// <summary>branch refを期待値付きで更新します。</summary>
    public Task<GitCommandResult> UpdateBranchReferenceAsync(string repositoryRoot, string branch, string newHead, string expectedOldHead, CancellationToken cancellationToken);

    /// <summary>追跡refではなく実Remoteのbranch HEADを取得します。</summary>
    public Task<GitCommandResult> GetActualRemoteHeadAsync(string repositoryRoot, string remote, string branch, string repositoryId, CancellationToken cancellationToken);

    /// <summary>期待Remote HEADを指定してforce-with-lease pushします。</summary>
    public Task<GitCommandResult> ForcePushWithLeaseAsync(string repositoryRoot, string remote, string branch, string expectedRemoteHead, string repositoryId, CancellationToken cancellationToken);
}
