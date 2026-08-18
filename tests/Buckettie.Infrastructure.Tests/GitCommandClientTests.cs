using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitCommandClientTests
{
    private static readonly string AskPassPath = Path.GetFullPath("Buckettie.AskPass.exe");
    private readonly FakeProcessExecutor _executor = new();

    [Fact]
    public async Task PullFastForwardOnlyAsync_WhenCalled_UsesFixedArguments()
    {
        GitCommandClient client = CreateClient();

        GitCommandResult result = await client.PullFastForwardOnlyAsync(
            "repository-root",
            "origin",
            "develop",
            "buckettie",
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _executor.Request!.Arguments.Should().Equal(
            "-c",
            "safe.directory=repository-root",
            "-c",
            "credential.helper=",
            "-c",
            "http.extraHeader=",
            "pull",
            "--ff-only",
            "--",
            "origin",
            "develop");
        _executor.Request.Environment.Should().ContainKey("GIT_ASKPASS").WhoseValue.Should().Be(AskPassPath);
        _executor.Request.Environment.Should().ContainKey("BUCKETTIE_ASKPASS_REPOSITORY")
            .WhoseValue.Should().Be("buckettie");
    }

    [Fact]
    public async Task PushAsync_WhenCalled_UsesFixedArguments()
    {
        GitCommandClient client = CreateClient();

        await client.PushAsync(
            "repository-root",
            "origin",
            "develop",
            "buckettie",
            TestContext.Current.CancellationToken);

        _executor.Request!.Arguments.Should().Equal(
            "-c",
            "safe.directory=repository-root",
            "-c",
            "credential.helper=",
            "-c",
            "http.extraHeader=",
            "push",
            "--",
            "origin",
            "develop");
    }

    [Fact]
    public async Task GetRemoteUrlAsync_WhenCalled_UsesFixedArguments()
    {
        GitCommandClient client = CreateClient();

        await client.GetRemoteUrlAsync(
            "repository-root",
            "origin",
            TestContext.Current.CancellationToken);

        _executor.Request!.Arguments.Should().Equal(
            "-c", "safe.directory=repository-root", "remote", "get-url", "--", "origin");
    }

    [Fact]
    public async Task GetRemoteUrlAsync_WhenRepositoryRootUsesBackslashes_NormalizesSafeDirectoryToForwardSlashes()
    {
        GitCommandClient client = CreateClient();

        await client.GetRemoteUrlAsync(
            @"F:\Workspace\sazysoft\AI_prompt",
            "origin",
            TestContext.Current.CancellationToken);

        _executor.Request!.Arguments.Should().Equal(
            "-c",
            "safe.directory=F:/Workspace/sazysoft/AI_prompt",
            "remote",
            "get-url",
            "--",
            "origin");
    }

    [Fact]
    public async Task GetRemoteHeadAsync_WhenCalled_UsesFixedRemoteTrackingReference()
    {
        GitCommandClient client = CreateClient();

        await client.GetRemoteHeadAsync(
            "repository-root",
            "origin",
            "develop",
            TestContext.Current.CancellationToken);

        _executor.Request!.Arguments.Should().Equal(
            "-c", "safe.directory=repository-root",
            "rev-parse", "--verify", "--end-of-options", "refs/remotes/origin/develop");
    }

    [Fact]
    public async Task GetAheadBehindAsync_WhenCalled_UsesFixedRemoteTrackingReference()
    {
        GitCommandClient client = CreateClient();

        await client.GetAheadBehindAsync(
            "repository-root",
            "origin",
            "develop",
            TestContext.Current.CancellationToken);

        _executor.Request!.Arguments.Should().Equal(
            "-c", "safe.directory=repository-root",
            "rev-list", "--left-right", "--count", "HEAD...refs/remotes/origin/develop");
    }

    [Fact]
    public async Task FetchAsync_WhenProcessTimesOut_ReturnsTimeout()
    {
        _executor.Result = new(null, string.Empty, string.Empty, false, true, false);
        GitCommandClient client = CreateClient();

        GitCommandResult result = await client.FetchAsync(
            "repository-root",
            "origin",
            "buckettie",
            TestContext.Current.CancellationToken);

        result.Failure.Should().Be(GitCommandFailure.TimedOut);
    }

    [Fact]
    public async Task GetStatusAsync_WhenCalled_DoesNotAttachAskPassEnvironment()
    {
        GitCommandClient client = CreateClient();

        await client.GetStatusAsync("repository-root", TestContext.Current.CancellationToken);

        _executor.Request!.Environment.Should().BeEmpty();
    }

    private GitCommandClient CreateClient() => new(
        _executor,
        TimeSpan.FromSeconds(10),
        AskPassPath,
        "developer");

    private sealed class FakeProcessExecutor : IProcessExecutor
    {
        internal ProcessRequest? Request { get; private set; }

        internal ProcessExecutionResult Result { get; set; } = new(0, string.Empty, string.Empty, false, false, false);

        public Task<ProcessExecutionResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Result);
        }
    }
}
