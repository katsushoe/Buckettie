namespace Buckettie.Application.Repositories;

/// <summary>
/// Git Remote URLが設定済みBitbucket Repositoryと一致するか検証します。
/// </summary>
public sealed class BitbucketRemoteUrlValidator
{
    private const string BitbucketHost = "bitbucket.org";

    /// <summary>
    /// HTTPS形式のRemote URLを検証します。
    /// </summary>
    public RepositoryValidationResult Validate(string workspace, string slug, string remoteUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);

        if (!TryParse(remoteUrl, out RemoteRepository? remote) || remote is null)
        {
            return RepositoryValidationResult.Invalid(RepositoryValidationError.RemoteUrlInvalid);
        }

        bool matches = string.Equals(remote.Workspace, workspace, StringComparison.Ordinal)
            && string.Equals(remote.Slug, slug, StringComparison.Ordinal);

        return matches
            ? RepositoryValidationResult.Valid()
            : RepositoryValidationResult.Invalid(RepositoryValidationError.RemoteMismatch);
    }

    private static bool TryParse(string remoteUrl, out RemoteRepository? remote)
    {
        remote = null;
        string normalized = remoteUrl.StartsWith("git@bitbucket.org:", StringComparison.OrdinalIgnoreCase)
            ? $"ssh://git@bitbucket.org/{remoteUrl["git@bitbucket.org:".Length..]}"
            : remoteUrl;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri) || !IsAllowedUri(uri))
        {
            return false;
        }

        string[] parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        string repositorySlug = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^4]
            : parts[1];
        if (repositorySlug.Length == 0)
        {
            return false;
        }

        remote = new RemoteRepository(parts[0], repositorySlug);
        return true;
    }

    private static bool IsAllowedUri(Uri uri)
    {
        if (!string.Equals(uri.Host, BitbucketHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.IsDefaultPort;
    }

    private sealed record RemoteRepository(string Workspace, string Slug);
}
