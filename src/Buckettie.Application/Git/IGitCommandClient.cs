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

    /// <summary>作業ツリー状態を取得します。</summary>
    public Task<GitCommandResult> GetStatusAsync(string repositoryRoot, CancellationToken cancellationToken);

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
}
