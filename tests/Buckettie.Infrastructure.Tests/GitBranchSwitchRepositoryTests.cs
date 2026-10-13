using System.Diagnostics;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitBranchSwitchRepositoryTests
{
    [Fact]
    public async Task BranchSwitchCommands_WithRealGit_SwitchOnlyExistingLocalBranchesInTheTargetRepository()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuckettieBranchSwitchTests", Guid.NewGuid().ToString("N"));
        CancellationToken token = TestContext.Current.CancellationToken;
        string target = Path.Combine(root, "target");
        string other = Path.Combine(root, "other");
        try
        {
            await InitializeAsync(target, token);
            await InitializeAsync(other, token);
            await RunGitAsync(target, token, "branch", "feature/test");
            await RunGitAsync(target, token, "update-ref", "refs/remotes/origin/remote-only", "HEAD");
            GitCommandClient client = new(TimeSpan.FromSeconds(10), Path.GetFullPath("Buckettie.AskPass.exe"), "developer");

            (await client.CheckBranchNameAsync(target, "feature/test", token)).IsSuccess.Should().BeTrue();
            (await client.CheckBranchNameAsync(target, "bad..name", token)).IsSuccess.Should().BeFalse();
            (await client.GetLocalBranchReferenceAsync(target, "feature/test", token)).IsSuccess.Should().BeTrue();
            // A remote-tracking ref alone is not a local branch, and switch must not create one from it.
            (await client.GetLocalBranchReferenceAsync(target, "remote-only", token)).Failure
                .Should().Be(GitCommandFailure.ReferenceNotFound);
            (await client.SwitchBranchAsync(target, "remote-only", token)).IsSuccess.Should().BeFalse();

            GitCommandResult switched = await client.SwitchBranchAsync(target, "feature/test", token);

            switched.IsSuccess.Should().BeTrue(switched.StandardError);
            (await client.GetCurrentBranchAsync(target, token)).StandardOutput.Trim().Should().Be("feature/test");
            (await client.GetCurrentBranchAsync(other, token)).StandardOutput.Trim().Should().Be("develop");
            (await RunGitAsync(target, token, "branch", "--list", "remote-only")).Should().BeEmpty();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task InitializeAsync(string repository, CancellationToken token)
    {
        Directory.CreateDirectory(repository);
        await RunGitAsync(repository, token, "init", "--initial-branch=develop");
        await RunGitAsync(repository, token, "-c", "user.name=Test", "-c", "user.email=test@example.com",
            "commit", "--allow-empty", "-m", "initial");
    }

    private static async Task<string> RunGitAsync(string workingDirectory, CancellationToken token, params string[] arguments)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        string output = await process.StandardOutput.ReadToEndAsync(token);
        string error = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        process.ExitCode.Should().Be(0, error);
        return output;
    }
}
