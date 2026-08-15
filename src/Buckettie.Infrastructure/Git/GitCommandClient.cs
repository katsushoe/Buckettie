using Buckettie.Application.Git;

namespace Buckettie.Infrastructure.Git;

/// <summary>
/// shellを介さず固定Gitコマンドだけを実行します。
/// </summary>
public sealed class GitCommandClient : IGitCommandClient
{
    private const string GitExecutable = "git";
    private readonly IProcessExecutor _executor;
    private readonly TimeSpan _commandTimeout;

    /// <summary>Git Clientを指定timeoutで初期化します。</summary>
    public GitCommandClient(TimeSpan commandTimeout)
        : this(new ProcessExecutor(), commandTimeout)
    {
    }

    internal GitCommandClient(IProcessExecutor executor, TimeSpan commandTimeout)
    {
        ArgumentNullException.ThrowIfNull(executor);
        if (commandTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeout));
        }

        _executor = executor;
        _commandTimeout = commandTimeout;
    }

    /// <inheritdoc />
    public Task<GitCommandResult> GetCurrentBranchAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["rev-parse", "--abbrev-ref", "HEAD"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetHeadAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["rev-parse", "HEAD"], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> GetStatusAsync(string repositoryRoot, CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["status", "--porcelain=v1"], cancellationToken);

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
        CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["fetch", "--", remote], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> PullFastForwardOnlyAsync(
        string repositoryRoot,
        string remote,
        string branch,
        CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["pull", "--ff-only", "--", remote, branch], cancellationToken);

    /// <inheritdoc />
    public Task<GitCommandResult> PushAsync(
        string repositoryRoot,
        string remote,
        string branch,
        CancellationToken cancellationToken) =>
        ExecuteAsync(repositoryRoot, ["push", "--", remote, branch], cancellationToken);

    private async Task<GitCommandResult> ExecuteAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ProcessRequest request = new(GitExecutable, repositoryRoot, arguments, _commandTimeout);
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

        return result.ExitCode == 0
            ? GitCommandResult.Success(result.StandardOutput, result.StandardError)
            : GitCommandResult.Failed(GitCommandFailure.Failed, result.StandardError);
    }
}
