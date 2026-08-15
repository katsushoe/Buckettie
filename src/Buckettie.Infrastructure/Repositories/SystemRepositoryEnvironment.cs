using Buckettie.Application.Repositories;

namespace Buckettie.Infrastructure.Repositories;

/// <summary>
/// OSのファイルシステムからRepository情報を取得します。
/// </summary>
public sealed class SystemRepositoryEnvironment : IRepositoryEnvironment
{
    /// <inheritdoc />
    public string GetFullPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public bool GitMetadataExists(string repositoryRoot)
    {
        string gitPath = Path.Combine(repositoryRoot, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    /// <inheritdoc />
    public bool ContainsReparsePoint(string path)
    {
        DirectoryInfo? current = new(path);
        while (current is not null)
        {
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
