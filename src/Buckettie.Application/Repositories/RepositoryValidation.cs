namespace Buckettie.Application.Repositories;

/// <summary>
/// Repository境界の検証エラーです。
/// </summary>
public enum RepositoryValidationError
{
    RepositoryNotAllowed,
    LocalRootNotFound,
    GitMetadataNotFound,
    LocalPathMismatch,
    LocalPathReparsePoint,
    RemoteUrlInvalid,
    RemoteMismatch,
}

/// <summary>
/// Repository境界の検証結果を表します。
/// </summary>
public sealed record RepositoryValidationResult(bool IsValid, RepositoryValidationError? Error)
{
    /// <summary>成功結果を生成します。</summary>
    public static RepositoryValidationResult Valid() => new(true, null);

    /// <summary>失敗結果を生成します。</summary>
    public static RepositoryValidationResult Invalid(RepositoryValidationError error) => new(false, error);
}
