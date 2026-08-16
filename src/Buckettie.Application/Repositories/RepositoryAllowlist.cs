using Buckettie.Application.Configuration;

namespace Buckettie.Application.Repositories;

/// <summary>
/// 設定済みRepositoryだけをIDから解決します。
/// </summary>
public sealed class RepositoryAllowlist
{
    private readonly IReadOnlyDictionary<string, RepositoryOptions> _repositories;

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
}
