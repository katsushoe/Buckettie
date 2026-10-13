using System.Diagnostics;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitSelectedCommitRepositoryTests
{
    [Fact]
    public async Task SelectedCommit_CommitsOneHunkAndPreservesUnselectedWorkingTreeHunk()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuckettieSelectedCommitTests", Guid.NewGuid().ToString("N"));
        string repository = Path.Combine(root, "repository");
        CancellationToken token = TestContext.Current.CancellationToken;
        GitCommandClient client = new(TimeSpan.FromSeconds(10), Path.GetFullPath("Buckettie.AskPass.exe"), "developer");
        try
        {
            Directory.CreateDirectory(repository);
            await RunGitAsync(repository, token, "init", "--initial-branch=develop");
            await RunGitAsync(repository, token, "config", "user.name", "Test");
            await RunGitAsync(repository, token, "config", "user.email", "test@example.com");
            string file = Path.Combine(repository, "notes.txt");
            await File.WriteAllTextAsync(file, "first\nsecond\n", token);
            await RunGitAsync(repository, token, "add", "--all");
            await RunGitAsync(repository, token, "commit", "-m", "base");
            string expectedHead = (await client.GetHeadAsync(repository, token)).StandardOutput.Trim();
            await File.WriteAllTextAsync(file, "selected\nother\n", token);
            string patch = "diff --git a/notes.txt b/notes.txt\n--- a/notes.txt\n+++ b/notes.txt\n@@ -1,2 +1,2 @@\n-first\n+selected\n second\n";

            GitCommandResult result = await client.CommitSelectedAsync(repository, "selected hunk", new("Test", "test@example.com"), expectedHead, patch, token);

            result.IsSuccess.Should().BeTrue(result.StandardError);
            (await RunGitAsync(repository, token, "show", "HEAD:notes.txt")).Should().Be("selected\nsecond\n");
            (await File.ReadAllTextAsync(file, token)).Should().Be("selected\nother\n");
            (await client.GetStatusAsync(repository, token)).StandardOutput.Should().Contain("notes.txt");
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
