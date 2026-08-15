using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitCommandClientTests
{
    private readonly FakeProcessExecutor _executor = new();

    [Fact]
    public async Task PullFastForwardOnlyAsync_WhenCalled_UsesFixedArguments()
    {
        GitCommandClient client = new(_executor, TimeSpan.FromSeconds(10));

        GitCommandResult result = await client.PullFastForwardOnlyAsync(
            "repository-root",
            "origin",
            "develop",
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _executor.Request!.Arguments.Should().Equal("pull", "--ff-only", "--", "origin", "develop");
    }

    [Fact]
    public async Task PushAsync_WhenCalled_UsesFixedArguments()
    {
        GitCommandClient client = new(_executor, TimeSpan.FromSeconds(10));

        await client.PushAsync(
            "repository-root",
            "origin",
            "develop",
            TestContext.Current.CancellationToken);

        _executor.Request!.Arguments.Should().Equal("push", "--", "origin", "develop");
    }

    [Fact]
    public async Task GetRemoteUrlAsync_WhenCalled_UsesFixedArguments()
    {
        GitCommandClient client = new(_executor, TimeSpan.FromSeconds(10));

        await client.GetRemoteUrlAsync(
            "repository-root",
            "origin",
            TestContext.Current.CancellationToken);

        _executor.Request!.Arguments.Should().Equal("remote", "get-url", "--", "origin");
    }

    [Fact]
    public async Task FetchAsync_WhenProcessTimesOut_ReturnsTimeout()
    {
        _executor.Result = new(null, string.Empty, string.Empty, false, true, false);
        GitCommandClient client = new(_executor, TimeSpan.FromSeconds(10));

        GitCommandResult result = await client.FetchAsync(
            "repository-root",
            "origin",
            TestContext.Current.CancellationToken);

        result.Failure.Should().Be(GitCommandFailure.TimedOut);
    }

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
