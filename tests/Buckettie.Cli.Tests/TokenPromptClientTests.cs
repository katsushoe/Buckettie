using System.Diagnostics;
using System.IO.Pipes;
using Buckettie.Application.Interactive;
using FluentAssertions;
using Xunit;

namespace Buckettie.Cli.Tests;

public sealed class TokenPromptClientTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("pipe-only-test-token")]
    public async Task ReadToken_WhenPipeResponds_ReturnsTokenWithoutCommandLineExposure(string? token)
    {
        Task? writer = null;
        string? result = await TokenPromptClient.ReadTokenCoreAsync("example", "https://bitbucket.org/workspace/repository.git",
            "en-US", TestContext.Current.CancellationToken, start =>
            {
                start.ArgumentList.Should().HaveCount(5);
                if (token is not null) start.ArgumentList.Should().NotContain(token);
                start.UseShellExecute.Should().BeFalse();
                string pipeName = start.ArgumentList[1];
                writer = Task.Run(async () =>
                {
                    await using NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(5000, TestContext.Current.CancellationToken);
                    await ApprovalPipeProtocol.WriteFrameAsync(pipe, new TokenPromptResponse(token), TestContext.Current.CancellationToken);
                }, TestContext.Current.CancellationToken);
                return Process.GetCurrentProcess();
            });
        await writer!;
        result.Should().Be(token);
    }

    [Fact]
    public async Task ReadToken_WhenLaunchFails_ReturnsSanitizedFailure()
    {
        Func<Task> action = () => TokenPromptClient.ReadTokenCoreAsync("example", "https://bitbucket.org/workspace/repository.git",
            "en-US", TestContext.Current.CancellationToken,
            _ => throw new System.ComponentModel.Win32Exception("sensitive diagnostic"));
        await action.Should().ThrowAsync<TokenPromptException>().WithMessage("TokenPromptLaunchFailed");
    }

    [Fact]
    public async Task ReadToken_WhenResponseArrivesLate_StillReturnsToken()
    {
        Task? writer = null;
        string? result = await TokenPromptClient.ReadTokenCoreAsync("example", "https://bitbucket.org/workspace/repository.git",
            "en-US", TestContext.Current.CancellationToken, start =>
            {
                string pipeName = start.ArgumentList[1];
                writer = Task.Run(async () =>
                {
                    await using NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(5000, TestContext.Current.CancellationToken);
                    // Models a user who leaves the connected dialog open before answering.
                    await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
                    await ApprovalPipeProtocol.WriteFrameAsync(pipe, new TokenPromptResponse("late-token"), TestContext.Current.CancellationToken);
                }, TestContext.Current.CancellationToken);
                return Process.GetCurrentProcess();
            });
        await writer!;
        result.Should().Be("late-token");
    }

    [Fact]
    public async Task ReadToken_WhenDialogExitsWithoutConnecting_ReturnsLaunchFailed()
    {
        Func<Task> action = () => TokenPromptClient.ReadTokenCoreAsync("example", "https://bitbucket.org/workspace/repository.git",
            "en-US", TestContext.Current.CancellationToken,
            _ => Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 3") { UseShellExecute = false, CreateNoWindow = true }));
        await action.Should().ThrowAsync<TokenPromptException>().WithMessage("TokenPromptLaunchFailed");
    }

    [Fact]
    public async Task ReadToken_WhenCallerCancels_ReturnsCancelled()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));
        Func<Task> action = () => TokenPromptClient.ReadTokenCoreAsync("example", "https://bitbucket.org/workspace/repository.git",
            "en-US", cancellation.Token, _ => Process.GetCurrentProcess());
        await action.Should().ThrowAsync<TokenPromptException>().WithMessage("TokenPromptCancelled");
    }
}
