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
    public async Task<GitCommandResult> GetCurrentBranchAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        GitCommandResult current = await ExecuteAsync(
            repositoryRoot, ["rev-parse", "--abbrev-ref", "HEAD"], cancellationToken).ConfigureAwait(false);
        if (current.IsSuccess && !string.Equals(current.StandardOutput.Trim(), "HEAD", StringComparison.Ordinal))
        {
            return current;
        }

        // An unborn branch still has a symbolic HEAD; rev-parse may not resolve it until its first commit.
        GitCommandResult symbolic = await ExecuteAsync(
            repositoryRoot, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken).ConfigureAwait(false);
        return symbolic.IsSuccess ? symbolic : current;
    }

    /// <inheritdoc />
    public async Task<GitCommandResult> GetHeadAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        GitCommandResult result = await ExecuteAsync(
            repositoryRoot,
            ["rev-parse", "--verify", "--quiet", "--end-of-options", "HEAD"],
            cancellationToken,
            missingReferenceAllowed: true).ConfigureAwait(false);
        return result.Failure == GitCommandFailure.ReferenceNotFound
            ? GitCommandResult.Success(string.Empty)
            : result;
    }

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
    public async Task<GitCommandResult> GetDiffAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        GitCommandResult head = await GetHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!head.IsSuccess)
        {
            return head;
        }

        string[] arguments = string.IsNullOrEmpty(head.StandardOutput.Trim())
            ? ["diff", "--no-ext-diff", "--binary", "--cached", "--"]
            : ["diff", "--no-ext-diff", "--binary", "HEAD", "--"];
        return await ExecuteAsync(repositoryRoot, arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<GitCommandResult> StageAllAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["add", "--all", "--"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> CommitAsync(
        string repositoryRoot,
        string message,
        GitCommitAuthor author,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(author);
        if (!GitCommitAuthor.IsValid(author.Name, author.Email))
            throw new ArgumentException("Invalid commit author.", nameof(author));
        // Environment variables keep the identity out of argument parsing and override the
        // service account's (usually empty) Git identity for both author and committer.
        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["GIT_AUTHOR_NAME"] = author.Name,
            ["GIT_AUTHOR_EMAIL"] = author.Email,
            ["GIT_COMMITTER_NAME"] = author.Name,
            ["GIT_COMMITTER_EMAIL"] = author.Email,
        };
        return ExecuteAsync(repositoryRoot, ["commit", "--message", message, "--"], cancellationToken, environment);
    }

    /// <inheritdoc />
    public async Task<GitCommandResult> CommitSelectedAsync(
        string repositoryRoot,
        string message,
        GitCommitAuthor author,
        string expectedHead,
        string patch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(author);
        if (!GitCommitAuthor.IsValid(author.Name, author.Email))
            throw new ArgumentException("Invalid commit author.", nameof(author));
        ArgumentException.ThrowIfNullOrWhiteSpace(patch);

        string temporaryIndex = Path.Combine(Path.GetTempPath(), $"buckettie-selected-commit-{Guid.NewGuid():N}.index");
        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["GIT_INDEX_FILE"] = temporaryIndex,
            ["GIT_AUTHOR_NAME"] = author.Name,
            ["GIT_AUTHOR_EMAIL"] = author.Email,
            ["GIT_COMMITTER_NAME"] = author.Name,
            ["GIT_COMMITTER_EMAIL"] = author.Email,
        };

        try
        {
            GitCommandResult initialize = string.IsNullOrEmpty(expectedHead)
                ? await ExecuteAsync(repositoryRoot, ["read-tree", "--empty"], cancellationToken, environment).ConfigureAwait(false)
                : await ExecuteAsync(repositoryRoot, ["read-tree", expectedHead], cancellationToken, environment).ConfigureAwait(false);
            if (!initialize.IsSuccess) return initialize;

            GitCommandResult check = await ExecuteAsync(repositoryRoot, ["apply", "--cached", "--check"],
                cancellationToken, environment, standardInput: patch).ConfigureAwait(false);
            if (!check.IsSuccess) return check;

            GitCommandResult apply = await ExecuteAsync(repositoryRoot, ["apply", "--cached"],
                cancellationToken, environment, standardInput: patch).ConfigureAwait(false);
            if (!apply.IsSuccess) return apply;

           GitCommandResult currentHead = await GetHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
           string observedHead = currentHead.IsSuccess ? currentHead.StandardOutput.Trim() : string.Empty;
           if ((!currentHead.IsSuccess && currentHead.Failure != GitCommandFailure.ReferenceNotFound) ||
               !string.Equals(observedHead, expectedHead, StringComparison.OrdinalIgnoreCase))
               return GitCommandResult.Failed(GitCommandFailure.ReferenceNotFound, "HEAD changed before selected commit.");

            GitCommandResult commit = await ExecuteAsync(repositoryRoot, ["commit", "--message", message, "--"],
                cancellationToken, environment).ConfigureAwait(false);
            if (!commit.IsSuccess) return commit;

            // Keep unselected worktree edits while advancing the real index to the new HEAD.
            return await ExecuteAsync(repositoryRoot, ["read-tree", "HEAD"], cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (string path in new[] { temporaryIndex, $"{temporaryIndex}.lock" })
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    /// <inheritdoc />
    public Task<GitCommandResult> PushStashAsync(
        string repositoryRoot, string message, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<string> arguments = ["stash", "push", "--include-untracked", "--message", message];
        if (paths.Count > 0)
        {
            arguments.Add("--");
            arguments.AddRange(paths.Select(path => $":(literal){path}"));
        }
        return ExecuteAsync(repositoryRoot, arguments, cancellationToken);
    }

    /// <inheritdoc />
    public Task<GitCommandResult> ListStashesAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["stash", "list", "--format=%H%x1f%gs"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> ApplyStashAsync(
        string repositoryRoot, string stashId, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["stash", "apply", "--index", stashId], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> DropStashAsync(
        string repositoryRoot, string selector, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["stash", "drop", selector], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetConfigValueAsync(
        string repositoryRoot,
        string key,
        CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["config", "--get", "--", key], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> ListRemoteUrlsAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["config", "--get-regexp", "^remote\\..*\\.url$"], cancellationToken,
            missingReferenceAllowed: true);

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
