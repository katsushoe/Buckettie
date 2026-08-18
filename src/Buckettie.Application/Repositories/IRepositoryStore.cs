using Buckettie.Application.Configuration;

namespace Buckettie.Application.Repositories;

/// <summary>
/// Repository設定の永続化Storeです。RepositoryAllowlistのIn-memory状態とは独立して
/// Register/Unregister/Updateの都度、この境界だけを書き換えます。
/// </summary>
public interface IRepositoryStore
{
    /// <summary>
    /// 保存済みRepository設定をすべて読み込みます。
    /// </summary>
    Task<IReadOnlyDictionary<string, RepositoryOptions>> LoadAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 新規Repositoryを追加します。同じIDが既に存在する場合はfalseを返します。
    /// </summary>
    Task<bool> InsertAsync(string repositoryId, RepositoryOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 既存Repositoryの設定を更新します。IDが存在しない場合はfalseを返します。
    /// </summary>
    Task<bool> UpdateAsync(string repositoryId, RepositoryOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 既存Repositoryを削除します。IDが存在しない場合はfalseを返します。
    /// </summary>
    Task<bool> DeleteAsync(string repositoryId, CancellationToken cancellationToken);
}
