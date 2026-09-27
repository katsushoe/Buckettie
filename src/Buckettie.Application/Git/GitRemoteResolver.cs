using Buckettie.Application.Repositories;
using System.Text.RegularExpressions;

namespace Buckettie.Application.Git;

/// <summary>Git Remote解決の結果種別です。</summary>
public enum GitRemoteResolutionStatus
{
    Resolved,
    NotFound,
    Ambiguous,
    Mismatch,
    SshRemoteNotSupported,
    CommandFailed,
}

/// <summary>Git Remote解決の結果です。</summary>
public sealed record GitRemoteResolution(
    GitRemoteResolutionStatus Status,
    string? Name,
    string? Url,
    GitCommandResult? FailedCommand)
{
    /// <summary>解決できたかを示します。</summary>
    public bool IsResolved => Status == GitRemoteResolutionStatus.Resolved;

    internal static GitRemoteResolution Resolved(string name, string url) => new(GitRemoteResolutionStatus.Resolved, name, url, null);

    internal static GitRemoteResolution Failed(GitRemoteResolutionStatus status) => new(status, null, null, null);

    internal static GitRemoteResolution CommandFailed(GitCommandResult result) =>
        new(GitRemoteResolutionStatus.CommandFailed, null, null, result);
}

/// <summary>ローカルRepositoryに設定されたHTTPS形式のBitbucket Remoteです。</summary>
public sealed record GitRemoteCandidate(string Name, string Url, string Workspace, string Slug);

/// <summary>
/// Moyai Repository Provider Contract（remote_resolution version 1）に従い、
/// 操作に使うGit Remoteを解決します。
/// </summary>
/// <remarks>
/// Remote名が指定されていればそのRemoteを使い、URLがWorkspace/Slugと一致することを検証します。
/// 指定がなければ、URLが一致するHTTPS Remoteを自動で選びます。候補が複数なら命名規則
/// <c>&lt;ホスト&gt;-origin-&lt;接続方式&gt;</c>に合う名前が1つだけの場合に限り選びます。
/// 既定値<c>origin</c>への暗黙の退避は行いません。SSH Remoteは候補から除外します。
/// </remarks>
public sealed class GitRemoteResolver
{
    /// <summary>provider_capabilitiesで表明するremote_resolutionのversionです。</summary>
    public const int ContractVersion = 1;

    /// <summary>provider_capabilitiesで表明するremote_resolutionのmodeです。</summary>
    public const string ContractMode = "repository_url";

    private static readonly Regex RemoteNamePattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.CultureInvariant);
    private static readonly Regex NamingRulePattern = new(
        "^[a-z0-9]+-origin-(?:https|ssh)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private readonly IGitCommandClient _git;

    /// <summary>Resolverを初期化します。</summary>
    public GitRemoteResolver(IGitCommandClient git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    /// <summary>Remote名がGit Remote名として受理できる形式か判定します。</summary>
    public static bool IsValidRemoteName(string? name) => name is not null && RemoteNamePattern.IsMatch(name);

    /// <summary>Remote名が推奨命名規則に合うか判定します。</summary>
    public static bool MatchesNamingRule(string name) => NamingRulePattern.IsMatch(name);

    /// <summary>指定Workspace/Slugに対応するRemoteを解決します。</summary>
    public async Task<GitRemoteResolution> ResolveAsync(
        string localRoot,
        string workspace,
        string slug,
        string? requestedName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return await ResolveNamedAsync(localRoot, workspace, slug, requestedName, cancellationToken)
                .ConfigureAwait(false);
        }

        CandidateListing listing = await ListCandidatesAsync(localRoot, cancellationToken).ConfigureAwait(false);
        if (listing.FailedCommand is not null) return GitRemoteResolution.CommandFailed(listing.FailedCommand);
        GitRemoteCandidate[] matches = listing.Candidates
            .Where(candidate => string.Equals(candidate.Workspace, workspace, StringComparison.Ordinal)
                && string.Equals(candidate.Slug, slug, StringComparison.Ordinal))
            .ToArray();
        return Select(matches);
    }

    /// <summary>
    /// 登録用に、Remote名の指定がない場合の候補を解決します。Workspace/Slugは候補のURLから導出し、
    /// 異なるRepositoryを指すRemoteが混在する場合は曖昧として拒否します。
    /// </summary>
    public async Task<GitRemoteResolution> DiscoverAsync(string localRoot, CancellationToken cancellationToken)
    {
        CandidateListing listing = await ListCandidatesAsync(localRoot, cancellationToken).ConfigureAwait(false);
        if (listing.FailedCommand is not null) return GitRemoteResolution.CommandFailed(listing.FailedCommand);
        int repositories = listing.Candidates
            .Select(candidate => (candidate.Workspace, candidate.Slug))
            .Distinct()
            .Count();
        if (repositories > 1) return GitRemoteResolution.Failed(GitRemoteResolutionStatus.Ambiguous);
        return Select(listing.Candidates);
    }

    private async Task<GitRemoteResolution> ResolveNamedAsync(
        string localRoot, string workspace, string slug, string name, CancellationToken cancellationToken)
    {
        if (!IsValidRemoteName(name)) return GitRemoteResolution.Failed(GitRemoteResolutionStatus.NotFound);
        GitCommandResult result = await _git.GetRemoteUrlAsync(localRoot, name, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Failure == GitCommandFailure.Failed
                && result.StandardError.Contains("No such remote", StringComparison.OrdinalIgnoreCase)
                ? GitRemoteResolution.Failed(GitRemoteResolutionStatus.NotFound)
                : GitRemoteResolution.CommandFailed(result);
        }

        string url = result.StandardOutput.Trim();
        if (BitbucketRemoteUrlValidator.IsSshRemote(url))
        {
            return GitRemoteResolution.Failed(GitRemoteResolutionStatus.SshRemoteNotSupported);
        }

        bool matches = BitbucketRemoteUrlValidator.TryParse(url, out BitbucketRemoteUrlValidator.BitbucketRemoteCoordinates? remote)
            && remote is not null
            && string.Equals(remote.Workspace, workspace, StringComparison.Ordinal)
            && string.Equals(remote.Slug, slug, StringComparison.Ordinal);
        return matches
            ? GitRemoteResolution.Resolved(name, url)
            : GitRemoteResolution.Failed(GitRemoteResolutionStatus.Mismatch);
    }

    private static GitRemoteResolution Select(IReadOnlyList<GitRemoteCandidate> matches)
    {
        if (matches.Count == 0) return GitRemoteResolution.Failed(GitRemoteResolutionStatus.NotFound);
        if (matches.Count == 1) return GitRemoteResolution.Resolved(matches[0].Name, matches[0].Url);
        GitRemoteCandidate[] named = matches.Where(candidate => MatchesNamingRule(candidate.Name)).ToArray();
        return named.Length == 1
            ? GitRemoteResolution.Resolved(named[0].Name, named[0].Url)
            : GitRemoteResolution.Failed(GitRemoteResolutionStatus.Ambiguous);
    }

    private async Task<CandidateListing> ListCandidatesAsync(string localRoot, CancellationToken cancellationToken)
    {
        GitCommandResult result = await _git.ListRemoteUrlsAsync(localRoot, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Failure == GitCommandFailure.ReferenceNotFound
                ? new CandidateListing([], null)
                : new CandidateListing([], result);
        }

        List<GitRemoteCandidate> candidates = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseConfigLine(line, out string name, out string url) || !seen.Add(name)) continue;
            if (BitbucketRemoteUrlValidator.IsSshRemote(url)) continue;
            if (!BitbucketRemoteUrlValidator.TryParse(url, out BitbucketRemoteUrlValidator.BitbucketRemoteCoordinates? remote)
                || remote is null)
            {
                continue;
            }

            candidates.Add(new GitRemoteCandidate(name, url, remote.Workspace, remote.Slug));
        }

        return new CandidateListing(candidates, null);
    }

    private static bool TryParseConfigLine(string line, out string name, out string url)
    {
        name = string.Empty;
        url = string.Empty;
        int separator = line.IndexOf(' ', StringComparison.Ordinal);
        if (separator <= 0) return false;
        string key = line[..separator];
        const string prefix = "remote.";
        const string suffix = ".url";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            || key.Length <= prefix.Length + suffix.Length)
        {
            return false;
        }

        name = key[prefix.Length..^suffix.Length];
        url = line[(separator + 1)..].Trim();
        return IsValidRemoteName(name) && url.Length > 0;
    }

    private sealed record CandidateListing(IReadOnlyList<GitRemoteCandidate> Candidates, GitCommandResult? FailedCommand);
}
