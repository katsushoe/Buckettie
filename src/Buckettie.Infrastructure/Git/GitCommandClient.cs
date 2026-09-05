using Buckettie.Application.Git;
using Buckettie.Application.Configuration;

namespace Buckettie.Infrastructure.Git;

/// <summary>
/// shellを介さず固定Gitコマンドだけを実行します。
/// </summary>
public sealed class GitCommandClient : IGitCommandClient
{
    private const string GitExecutable = "git";
    private readonly IProcessExecutor _executor;
    private readonly TimeSpan _commandTimeout;
    private readonly string _askPassExecutable;
    private readonly string _atlassianEmail;

    /// <summary>Git Clientを指定timeoutで初期化します。</summary>
    public GitCommandClient(
        TimeSpan commandTimeout,
        string askPassExecutable,
        string atlassianEmail)
        : this(new ProcessExecutor(), commandTimeout, askPassExecutable, atlassianEmail)
    {
    }

    internal GitCommandClient(
        IProcessExecutor executor,
        TimeSpan commandTimeout,
        string askPassExecutable,
        string atlassianEmail)
    {
        ArgumentNullException.ThrowIfNull(executor);
        if (commandTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeout));
        }

        _ = GitAskPassProtocol.CreateEnvironment(askPassExecutable, "validation", atlassianEmail);
        _executor = executor;
        _commandTimeout = commandTimeout;
        _askPassExecutable = askPassExecutable;
        _atlassianEmail = atlassianEmail;
    }

    /// <inheritdoc />
    public Task<GitCommandResult> GetCurrentBranchAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["rev-parse", "--abbrev-ref", "HEAD"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetHeadAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["rev-parse", "HEAD"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetRemoteHeadAsync(
        string repositoryRoot,
        string remote,
        string branch,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            repositoryRoot,
            ["rev-parse", "--verify", "--quiet", "--end-of-options", RemoteReference(remote, branch)],
            cancellationToken, missingReferenceAllowed: true);

    /// <inheritdoc />
    public Task<GitCommandResult> GetAheadBehindAsync(
        string repositoryRoot,
        string remote,
        string branch,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            repositoryRoot,
            ["rev-list", "--left-right", "--count", $"HEAD...{RemoteReference(remote, branch)}"],
            cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetStatusAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["status", "--porcelain=v1"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetDiffAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["diff", "--no-ext-diff", "--binary", "HEAD", "--"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> StageAllAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["add", "--all", "--"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> CommitAsync(
        string repositoryRoot,
        string message,
        CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["commit", "--message", message, "--"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetRemoteUrlAsync(
        string repositoryRoot,
        string remote,
        CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["remote", "get-url", "--", remote], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> FetchAsync(
        string repositoryRoot,
        string remote,
        string repositoryId,
        CancellationToken cancellationToken) =>
        ExecuteNetworkAsync(repositoryRoot, ["fetch", "--", remote], repositoryId, cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> PullFastForwardOnlyAsync(
        string repositoryRoot,
        string remote,
        string branch,
        string repositoryId,
        CancellationToken cancellationToken) =>
        ExecuteNetworkAsync(
            repositoryRoot,
            ["pull", "--ff-only", "--", remote, branch],
            repositoryId,
            cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> PushAsync(
        string repositoryRoot,
        string remote,
        string branch,
        string repositoryId,
        CancellationToken cancellationToken) =>
        ExecuteNetworkAsync(repositoryRoot, ["push", "--", remote, branch], repositoryId, cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> PushTagAsync(
        string repositoryRoot,
        string remote,
        string tag,
        string repositoryId,
        CancellationToken cancellationToken) =>
        ExecuteNetworkAsync(
            repositoryRoot,
            ["push", "--", remote, $"refs/tags/{tag}:refs/tags/{tag}"],
            repositoryId,
            cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetCommitMetadataAsync(
        string repositoryRoot, string commit, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot,
            ["show", "--no-patch", "--format=format:%H%x1f%T%x1f%P%x1f%an%x1f%ae%x1f%aI%x1f%cn%x1f%ce%x1f%cI%x1f%G?%x1f%B", "--end-of-options", commit],
            cancellationToken);

    /// <inheritdoc />
    public async Task<GitCommandResult> GetUnfinishedOperationAsync(
        string repositoryRoot, CancellationToken cancellationToken)
    {
        string[] markers = ["MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply"];
        List<string> active = [];
        foreach (string marker in markers)
        {
            GitCommandResult path = await ExecuteAsync(
                repositoryRoot, ["rev-parse", "--git-path", marker], cancellationToken).ConfigureAwait(false);
            if (!path.IsSuccess)
            {
                return path;
            }
            string resolved = path.StandardOutput.Trim();
            if (!Path.IsPathRooted(resolved)) resolved = Path.GetFullPath(resolved, repositoryRoot);
            if (File.Exists(resolved) || Directory.Exists(resolved))
            {
                active.Add(marker);
            }
        }
        return GitCommandResult.Success(string.Join(',', active));
    }

    /// <inheritdoc />
    public Task<GitCommandResult> CreateReferenceAsync(
        string repositoryRoot, string reference, string target, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["update-ref", "--create-reflog", reference, target], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> CreateCommitAsync(
        string repositoryRoot,
        string metadata,
        IReadOnlyDictionary<string, string> identityEnvironment,
        CancellationToken cancellationToken)
    {
        string[] parts = metadata.Split('\u001f', 3);
        if (parts.Length != 3)
        {
            return Task.FromResult(GitCommandResult.Failed(GitCommandFailure.Failed, "invalid commit metadata"));
        }
        List<string> arguments = ["commit-tree", parts[0]];
        foreach (string parent in parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            arguments.Add("-p");
            arguments.Add(parent);
        }
        arguments.Add("-F");
        arguments.Add("-");
        return ExecuteAsync(repositoryRoot, arguments, cancellationToken, identityEnvironment, standardInput: parts[2]);
    }

    /// <inheritdoc />
    public Task<GitCommandResult> UpdateBranchReferenceAsync(
        string repositoryRoot, string branch, string newHead, string expectedOldHead, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["update-ref", $"refs/heads/{branch}", newHead, expectedOldHead], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetActualRemoteHeadAsync(
        string repositoryRoot, string remote, string branch, string repositoryId, CancellationToken cancellationToken) =>
        ExecuteNetworkAsync(repositoryRoot, ["ls-remote", "--heads", "--", remote, $"refs/heads/{branch}"], repositoryId, cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> ForcePushWithLeaseAsync(
        string repositoryRoot, string remote, string branch, string expectedRemoteHead,
        string repositoryId, CancellationToken cancellationToken) =>
        ExecuteNetworkAsync(repositoryRoot,
            ["push", $"--force-with-lease=refs/heads/{branch}:{expectedRemoteHead}", "--", remote,
                $"refs/heads/{branch}:refs/heads/{branch}"], repositoryId, cancellationToken);

    private Task<GitCommandResult> ExecuteNetworkAsync(
        string repositoryRoot,
        IReadOnlyList<string> operationArguments,
        string repositoryId,
        CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "-c",
            "credential.helper=",
            "-c",
            "http.extraHeader=",
            .. operationArguments,
        ];
        IReadOnlyDictionary<string, string> environment = GitAskPassProtocol.CreateEnvironment(
            _askPassExecutable,
            repositoryId,
            _atlassianEmail);
        return ExecuteAsync(repositoryRoot, arguments, cancellationToken, environment);
    }

    private static string RemoteReference(string remote, string branch) => $"refs/remotes/{remote}/{branch}";

    private async Task<GitCommandResult> ExecuteAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null,
        bool missingReferenceAllowed = false,
        string? standardInput = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        string safeDirectory = repositoryRoot.Replace('\\', '/');
        string[] safeArguments = ["-c", $"safe.directory={safeDirectory}", .. arguments];
        ProcessRequest request = new(
            GitExecutable,
            repositoryRoot,
            safeArguments,
            environment ?? new Dictionary<string, string>(),
            _commandTimeout,
            standardInput);
        ProcessExecutionResult result = await _executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.NotFound)
        {
            return GitCommandResult.Failed(GitCommandFailure.NotFound);
        }

        if (result.TimedOut)
        {
            return GitCommandResult.Failed(GitCommandFailure.TimedOut, result.StandardError);
        }

        if (result.Cancelled)
        {
            return GitCommandResult.Failed(GitCommandFailure.Cancelled, result.StandardError);
        }

        if (missingReferenceAllowed && result.ExitCode == 1 && string.IsNullOrEmpty(result.StandardError))
        {
            return GitCommandResult.Failed(GitCommandFailure.ReferenceNotFound);
        }

        return result.ExitCode == 0
            ? GitCommandResult.Success(result.StandardOutput, result.StandardError)
            : GitCommandResult.Failed(GitCommandFailure.Failed, result.StandardError);
    }
}
