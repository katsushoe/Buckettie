using Buckettie.Application.Repositories;
using Buckettie.Application.Configuration;

namespace Buckettie.Application.Git;

/// <summary>
/// Git親processとBuckettie AskPass間の非secret protocolです。
/// </summary>
public static class GitAskPassProtocol
{
    /// <summary>AskPass executableの環境変数名です。</summary>
    public const string AskPassVariable = "GIT_ASKPASS";

    /// <summary>AskPass強制指定の環境変数名です。</summary>
    public const string AskPassRequireVariable = "GIT_ASKPASS_REQUIRE";

    /// <summary>Repository IDの環境変数名です。</summary>
    public const string RepositoryVariable = "BUCKETTIE_ASKPASS_REPOSITORY";

    /// <summary>Atlassian emailの環境変数名です。</summary>
    public const string UsernameVariable = "BUCKETTIE_ASKPASS_USERNAME";

    /// <summary>
    /// Git processへ渡す非secret環境変数を生成します。
    /// </summary>
    public static IReadOnlyDictionary<string, string> CreateEnvironment(
        string askPassExecutable,
        string repositoryId,
        string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(askPassExecutable);
        if (!Path.IsPathFullyQualified(askPassExecutable))
        {
            throw new ArgumentException("AskPass executable path must be absolute.", nameof(askPassExecutable));
        }

        if (!RepositoryId.IsLookupValid(repositoryId))
        {
            throw new ArgumentException("Invalid repository ID.", nameof(repositoryId));
        }

        if (!BitbucketUsername.IsValid(username))
        {
            throw new ArgumentException("Invalid Bitbucket username.", nameof(username));
        }
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AskPassVariable] = askPassExecutable,
            [AskPassRequireVariable] = "force",
            [RepositoryVariable] = repositoryId,
            [UsernameVariable] = username,
        };
    }
}
