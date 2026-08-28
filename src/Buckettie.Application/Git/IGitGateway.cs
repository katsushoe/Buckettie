namespace Buckettie.Application.Git;

/// <summary>
/// Repository IDだけを受け取る安全なGit操作境界です。
/// </summary>
public interface IGitGateway
{
    /// <summary>Repository状態を取得します。</summary>
    public Task<GitGatewayResult> GetStatusAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>設定済みRemoteからfetchします。</summary>
    public Task<GitGatewayResult> FetchAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>現在branchへfast-forward限定でpullします。</summary>
    public Task<GitGatewayResult> PullAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>許可された現在branchをpushします。</summary>
    public Task<GitGatewayResult> PushAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>Policy準拠のローカルTagをpushします。</summary>
    public Task<GitGatewayResult> PushTagAsync(
        string repository, string tag, CancellationToken cancellationToken = default);
}
