using Buckettie.Application.Configuration;

namespace Buckettie.Application.Repositories;

/// <summary>
/// 設定済みRepositoryだけをIDから解決します。
/// </summary>
public sealed class RepositoryAllowlist
{
    private readonly Lock _writeLock = new();
    private volatile IReadOnlyDictionary<string, RepositoryOptions> _repositories;

    /// <summary>
    /// Allowlistを初期化します。
    /// </summary>
    public RepositoryAllowlist(BuckettieOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Repositories);
        _repositories = new Dictionary<string, RepositoryOptions>(options.Repositories, StringComparer.Ordinal);
    }

    /// <summary>
    /// Repository IDに対応する設定を返します。
    /// </summary>
    public bool TryGet(string repositoryId, out RepositoryOptions? repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        return _repositories.TryGetValue(repositoryId, out repository);
    }

    /// <summary>
    /// 現在のAllowlist内容のSnapshotを返します（衝突検証・永続化用）。
    /// </summary>
    internal IReadOnlyDictionary<string, RepositoryOptions> Snapshot() => _repositories;

    /// <summary>
    /// 新規RepositoryをAllowlistへ追加します。既に同じIDが存在する場合は失敗します。
    /// </summary>
    internal bool Register(string repositoryId, RepositoryOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);
        lock (_writeLock)
        {
            if (_repositories.ContainsKey(repositoryId))
            {
                return false;
            }

            Dictionary<string, RepositoryOptions> next = new(_repositories, StringComparer.Ordinal)
            {
                [repositoryId] = options,
            };
            _repositories = next;
            return true;
        }
    }

    /// <summary>
    /// RepositoryをAllowlistから削除します。存在しない場合は失敗します。
    /// </summary>
    internal bool Unregister(string repositoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        lock (_writeLock)
        {
            if (!_repositories.ContainsKey(repositoryId))
            {
                return false;
            }

            Dictionary<string, RepositoryOptions> next = new(_repositories, StringComparer.Ordinal);
            next.Remove(repositoryId);
            _repositories = next;
            return true;
        }
    }

    /// <summary>
    /// 既存RepositoryのAllowlist設定を置き換えます。存在しない場合は失敗します。
    /// </summary>
    internal bool Update(string repositoryId, RepositoryOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);
        lock (_writeLock)
        {
            if (!_repositories.ContainsKey(repositoryId))
            {
                return false;
            }

            Dictionary<string, RepositoryOptions> next = new(_repositories, StringComparer.Ordinal)
            {
                [repositoryId] = options,
            };
            _repositories = next;
            return true;
        }
    }
}
