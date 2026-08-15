namespace Buckettie.Application.Repositories;

/// <summary>
/// Repositoryのローカルパス境界を検証します。
/// </summary>
public sealed class LocalPathValidator
{
    private readonly IRepositoryEnvironment _environment;

    /// <summary>
    /// 検証サービスを初期化します。
    /// </summary>
    public LocalPathValidator(IRepositoryEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <summary>
    /// 設定済みルートと操作対象ルートが同じ安全なGit Repositoryか検証します。
    /// </summary>
    public RepositoryValidationResult Validate(string configuredRoot, string candidateRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateRoot);

        string allowedPath = _environment.GetFullPath(configuredRoot);
        string candidatePath = _environment.GetFullPath(candidateRoot);

        if (!_environment.DirectoryExists(allowedPath))
        {
            return RepositoryValidationResult.Invalid(RepositoryValidationError.LocalRootNotFound);
        }

        if (!string.Equals(allowedPath, candidatePath, StringComparison.OrdinalIgnoreCase))
        {
            return RepositoryValidationResult.Invalid(RepositoryValidationError.LocalPathMismatch);
        }

        if (_environment.ContainsReparsePoint(allowedPath))
        {
            return RepositoryValidationResult.Invalid(RepositoryValidationError.LocalPathReparsePoint);
        }

        return _environment.GitMetadataExists(allowedPath)
            ? RepositoryValidationResult.Valid()
            : RepositoryValidationResult.Invalid(RepositoryValidationError.GitMetadataNotFound);
    }
}
