namespace Buckettie.Application.Repositories;

/// <summary>
/// RepositoryのOS依存情報を提供します。
/// </summary>
public interface IRepositoryEnvironment
{
    /// <summary>パスを絶対パスへ正規化します。</summary>
    public string GetFullPath(string path);

    /// <summary>Directoryが存在するかを返します。</summary>
    public bool DirectoryExists(string path);

    /// <summary>.gitメタデータが存在するかを返します。</summary>
    public bool GitMetadataExists(string repositoryRoot);

    /// <summary>パスまたは祖先にsymlink／junctionが含まれるかを返します。</summary>
    public bool ContainsReparsePoint(string path);
}
