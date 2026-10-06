using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;

namespace Buckettie.Server;

/// <summary>Moyai Assertion検証に使うRepositoryとMoyai Projectの対応付け（Binding）です。</summary>
/// <param name="ProjectId">Moyai Project ID。</param>
/// <param name="NormalizedRepository">登録済みWorkspace/Slugから導出した正規化Repository名。</param>
internal sealed record MoyaiBinding(Guid ProjectId, string NormalizedRepository);

/// <summary>
/// 設定ファイルの<c>provider_authentication.bindings</c>と、Repository登録に保存したMoyai Project IDから
/// Bindingを解決します。正規化Repository名は常に登録時にGit Remoteから導出したWorkspace/Slugを使い、
/// 呼び出し元の申告値は使いません。
/// </summary>
internal static class MoyaiBindings
{
    /// <summary>登録済みRepositoryの正規化Repository名です。</summary>
    internal static string Normalize(RepositoryOptions repository) =>
        $"bitbucket.org/{repository.Workspace}/{repository.Slug}";

    /// <summary>
    /// RepositoryのBindingを返します。設定ファイルのBindingを優先し、なければ登録に保存したProject IDを使います。
    /// どちらもなければnullです。
    /// </summary>
    internal static MoyaiBinding? Resolve(
        string repositoryId, RepositoryOptions registered, ProviderAuthenticationOptions? authentication)
    {
        ArgumentNullException.ThrowIfNull(registered);
        string normalized = Normalize(registered);
        ProviderProjectBinding? configured = authentication?.Bindings.SingleOrDefault(item =>
            string.Equals(item.Repository, repositoryId, StringComparison.OrdinalIgnoreCase));
        if (configured is not null) return new(configured.ProjectId, configured.NormalizedRepository);
        return registered.MoyaiProjectId is { } projectId ? new(projectId, normalized) : null;
    }

    /// <summary>
    /// 指定Project IDを、対象Repository以外のRepositoryまたは対象Repositoryの設定ファイルBindingが既に使っているかを返します。
    /// </summary>
    internal static bool Conflicts(
        Guid projectId, string repositoryId, RepositoryAllowlist allowlist, ProviderAuthenticationOptions? authentication)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        foreach ((string id, RepositoryOptions options) in allowlist.Snapshot())
        {
            if (string.Equals(id, repositoryId, StringComparison.OrdinalIgnoreCase)) continue;
            if (options.MoyaiProjectId == projectId) return true;
        }

        foreach (ProviderProjectBinding binding in authentication?.Bindings ?? [])
        {
            bool sameRepository = string.Equals(binding.Repository, repositoryId, StringComparison.OrdinalIgnoreCase);
            // The configuration file owns its bindings; a registry value must not contradict or duplicate them.
            if (binding.ProjectId == projectId ? !sameRepository : sameRepository) return true;
        }

        return false;
    }
}
