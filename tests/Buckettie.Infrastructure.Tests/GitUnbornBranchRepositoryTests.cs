using System.Diagnostics;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitUnbornBranchRepositoryTests
{
    [Fact]
    public async Task UnbornBranch_CanReadStatusCommitAndPushFirstBranchToEmptyBareRemote()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuckettieUnbornBranchTests", Guid.NewGuid().ToString("N"));
        string repository = Path.Combine(root, "repository");
        string remote = Path.Combine(root, "remote.git");
        CancellationToken token = TestContext.Current.CancellationToken;
        GitCommandClient client = new(TimeSpan.FromSeconds(10), Path.GetFullPath("Buckettie.AskPass.exe"), "developer");
        try
        {
            Directory.CreateDirectory(repository);
            await RunGitAsync(repository, token, "init", "--initial-branch=develop");
            await RunGitAsync(root, token, "init", "--bare", remote);
            await RunGitAsync(repository, token, "remote", "add", "origin", remote);
            await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "first commit\n", token);

            GitCommandResult branch = await client.GetCurrentBranchAsync(repository, token);
            GitCommandResult head = await client.GetHeadAsync(repository, token);
            GitCommandResult status = await client.GetStatusAsync(repository, token);

            branch.IsSuccess.Should().BeTrue(branch.StandardError);
            branch.StandardOutput.Trim().Should().Be("develop");
            head.IsSuccess.Should().BeTrue(head.StandardError);
            head.StandardOutput.Should().BeEmpty("an unborn branch has no commit object");
            status.IsSuccess.Should().BeTrue(status.StandardError);
            status.StandardOutput.Should().Contain("README.md");

            (await client.StageAllAsync(repository, token)).IsSuccess.Should().BeTrue();
            GitCommandResult diff = await client.GetDiffAsync(repository, token);
            diff.IsSuccess.Should().BeTrue(diff.StandardError);
            diff.StandardOutput.Should().Contain("first commit");

            GitCommandResult committed = await client.CommitAsync(
                repository,
                "feat: initial commit",
                new GitCommitAuthor("Neon SDK", "neon@example.com"),
                token);
            committed.IsSuccess.Should().BeTrue(committed.StandardError);
            GitCommandResult firstHead = await client.GetHeadAsync(repository, token);
            firstHead.IsSuccess.Should().BeTrue(firstHead.StandardError);
            firstHead.StandardOutput.Trim().Should().MatchRegex("^[0-9a-f]{40}$");

            GitCommandResult cleanStatus = await client.GetStatusAsync(repository, token);
            cleanStatus.IsSuccess.Should().BeTrue(cleanStatus.StandardError);
            cleanStatus.StandardOutput.Should().BeEmpty();
            GitCommandResult ordinaryDiff = await client.GetDiffAsync(repository, token);
            ordinaryDiff.IsSuccess.Should().BeTrue(ordinaryDiff.StandardError);
            ordinaryDiff.StandardOutput.Should().BeEmpty();

            GitCommandResult pushed = await client.PushAsync(repository, "origin", "develop", "neonsdk", token);
            pushed.IsSuccess.Should().BeTrue(pushed.StandardError);
            string remoteHead = (await RunGitAsync(root, token, "--git-dir", remote, "rev-parse", "--verify", "refs/heads/develop")).Trim();
            remoteHead.Should().Be(firstHead.StandardOutput.Trim());
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
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
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(token);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        string output = await outputTask;
        string error = await errorTask;
        process.ExitCode.Should().Be(0, error);
        return output;
    }
}
