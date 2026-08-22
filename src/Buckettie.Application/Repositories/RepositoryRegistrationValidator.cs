using Buckettie.Application.Configuration;
using Buckettie.Application.Git;

namespace Buckettie.Application.Repositories;

/// <summary>
/// 新規Repository登録候補を検証します。Workspace／SlugはGit Remoteから導出し、
/// 呼び出し元の申告値は信用しません。
/// </summary>
public sealed class RepositoryRegistrationValidator
{
    private readonly RepositoryAllowlist _allowlist;
    private readonly IRepositoryEnvironment _environment;
    private readonly IGitCommandClient _git;

    /// <summary>Validatorを初期化します。</summary>
    public RepositoryRegistrationValidator(
        RepositoryAllowlist allowlist,
        IRepositoryEnvironment environment,
        IGitCommandClient git)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(git);
        _allowlist = allowlist;
        _environment = environment;
        _git = git;
    }

    /// <summary>
    /// 登録候補を検証し、Git Remoteから導出したWorkspace／Slugを返します。
    /// </summary>
    public async Task<RepositoryRegistrationValidationResult> ValidateAsync(
        string repositoryId,
        string localRoot,
        string remote,
        CancellationToken cancellationToken)
    {
        if (!RepositoryId.IsValid(repositoryId))
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.RepositoryIdInvalid);
        }

        if (string.IsNullOrWhiteSpace(localRoot) || string.IsNullOrWhiteSpace(remote))
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.LocalRootNotFound);
        }

        IReadOnlyDictionary<string, RepositoryOptions> snapshot = _allowlist.Snapshot();
        if (snapshot.ContainsKey(repositoryId))
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.RepositoryAlreadyRegistered);
        }

        string fullRoot;
        try
        {
            fullRoot = _environment.GetFullPath(localRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.LocalRootNotFound);
        }

        if (!_environment.DirectoryExists(fullRoot))
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.LocalRootNotFound);
        }

        if (_environment.ContainsReparsePoint(fullRoot))
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.LocalPathReparsePoint);
        }

        if (!_environment.GitMetadataExists(fullRoot))
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.GitMetadataNotFound);
        }

        GitCommandResult remoteResult = await _git.GetRemoteUrlAsync(fullRoot, remote, cancellationToken)
            .ConfigureAwait(false);
        if (!remoteResult.IsSuccess)
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.RemoteUrlInvalid);
        }

        string remoteUrl = remoteResult.StandardOutput.Trim();
        if (BitbucketRemoteUrlValidator.IsSshRemote(remoteUrl))
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.SshRemoteNotSupported);
        }

        if (!BitbucketRemoteUrlValidator.TryParse(
                remoteUrl,
                out BitbucketRemoteUrlValidator.BitbucketRemoteCoordinates? coordinates)
            || coordinates is null)
        {
            return RepositoryRegistrationValidationResult.Invalid(
                RepositoryValidationError.RemoteUrlInvalid);
        }

        foreach (RepositoryOptions existing in snapshot.Values)
        {
            bool sameRemote = string.Equals(existing.Workspace, coordinates.Workspace, StringComparison.Ordinal)
                && string.Equals(existing.Slug, coordinates.Slug, StringComparison.Ordinal);
            bool sameRoot = string.Equals(
                _environment.GetFullPath(existing.LocalRoot),
                fullRoot,
                StringComparison.OrdinalIgnoreCase);
            if (sameRemote || sameRoot)
            {
                return RepositoryRegistrationValidationResult.Invalid(
                    RepositoryValidationError.RepositoryAlreadyRegistered);
            }
        }

        return RepositoryRegistrationValidationResult.Valid(
            coordinates.Workspace, coordinates.Slug, fullRoot, remoteUrl);
    }
}

/// <summary>Repository登録候補の検証結果を表します。</summary>
public sealed record RepositoryRegistrationValidationResult(
    bool IsValid,
    string? Workspace,
    string? Slug,
    string? LocalRoot,
    string? RemoteUrl,
    RepositoryValidationError? Error)
{
    /// <summary>成功結果を生成します。</summary>
    public static RepositoryRegistrationValidationResult Valid(
        string workspace, string slug, string localRoot, string remoteUrl) =>
        new(true, workspace, slug, localRoot, remoteUrl, null);

    /// <summary>失敗結果を生成します。</summary>
    public static RepositoryRegistrationValidationResult Invalid(RepositoryValidationError error) =>
        new(false, null, null, null, null, error);
}
