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

        if (IsSshRemote(remoteUrl))
        {
            return RepositoryValidationResult.Invalid(RepositoryValidationError.SshRemoteNotSupported);
        }

        if (!TryParse(remoteUrl, out BitbucketRemoteCoordinates? remote) || remote is null)
        {
            return RepositoryValidationResult.Invalid(RepositoryValidationError.RemoteUrlInvalid);
        }

        bool matches = string.Equals(remote.Workspace, workspace, StringComparison.Ordinal)
            && string.Equals(remote.Slug, slug, StringComparison.Ordinal);

        return matches
            ? RepositoryValidationResult.Valid()
            : RepositoryValidationResult.Invalid(RepositoryValidationError.RemoteMismatch);
    }

    /// <summary>
    /// HTTPS形式のBitbucket Remote URLからWorkspace/Slugを取り出します。
    /// </summary>
    public static bool TryParse(string remoteUrl, out BitbucketRemoteCoordinates? remote)
    {
        remote = null;
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out Uri? uri) || !IsAllowedUri(uri))
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

        remote = new BitbucketRemoteCoordinates(parts[0], repositorySlug);
        return true;
    }

    /// <summary>SSH形式のRemote URLか判定します。</summary>
    public static bool IsSshRemote(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl)) return false;
        if (remoteUrl.StartsWith("git@bitbucket.org:", StringComparison.OrdinalIgnoreCase)) return true;
        return Uri.TryCreate(remoteUrl, UriKind.Absolute, out Uri? uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeSsh, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>解析済みBitbucket Remoteの座標です。</summary>
    public sealed record BitbucketRemoteCoordinates(string Workspace, string Slug);
}
