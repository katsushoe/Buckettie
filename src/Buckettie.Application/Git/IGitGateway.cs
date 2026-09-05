namespace Buckettie.Application.Git;

/// <summary>
/// Repository IDだけを受け取る安全なGit操作境界です。
/// </summary>
public interface IGitGateway
{
    /// <summary>Repository状態を取得します。</summary>
    public Task<GitGatewayResult> GetStatusAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>RepositoryのHEADに対する作業ツリー差分を取得します。</summary>
    public Task<GitGatewayResult> GetDiffAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>Policyに従って作業ツリーの変更をlocal commitします。</summary>
    public Task<GitGatewayResult> CommitAsync(
        string repository, string message, CancellationToken cancellationToken = default);

    /// <summary>設定済みRemoteからfetchします。</summary>
    public Task<GitGatewayResult> FetchAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>現在branchへfast-forward限定でpullします。</summary>
    public Task<GitGatewayResult> PullAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>許可された現在branchをpushします。</summary>
    public Task<GitGatewayResult> PushAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>Policy準拠のローカルTagをpushします。</summary>
    public Task<GitGatewayResult> PushTagAsync(
        string repository, string tag, CancellationToken cancellationToken = default);

    /// <summary>最新commitのidentity書き換え内容を状態変更せず返します。</summary>
    public Task<GitGatewayResult> PreviewHistoryRewriteAsync(
        string repository, GitHistoryRewriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>最新commitのidentityを変更し、復旧refを保持します。</summary>
    public Task<GitGatewayResult> RewriteHistoryAsync(
        string repository, GitHistoryRewriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>実Remoteを照合してforce-with-lease pushします。</summary>
    public Task<GitGatewayResult> ForcePushWithLeaseAsync(
        string repository, GitForceWithLeaseRequest request, CancellationToken cancellationToken = default);
}
