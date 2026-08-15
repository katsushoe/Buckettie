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

    private async Task<GitCommandResult> ExecuteAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ProcessRequest request = new(
            GitExecutable,
            repositoryRoot,
            arguments,
            environment ?? new Dictionary<string, string>(),
            _commandTimeout);
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
