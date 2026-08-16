namespace Buckettie.Application.Credentials;

/// <summary>
/// Bitbucket API Tokenの安全な永続化境界です。
/// </summary>
public interface IApiTokenStore
{
    /// <summary>Repository IDに対応するTokenを保存します。</summary>
    public ApiTokenStoreResult Save(string repositoryId, string token);

    /// <summary>Repository IDに対応するTokenを取得します。</summary>
    public ApiTokenStoreResult Read(string repositoryId);

    /// <summary>Repository IDに対応するTokenを削除します。</summary>
    public ApiTokenStoreResult Delete(string repositoryId);
}
