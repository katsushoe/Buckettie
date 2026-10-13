using System.Diagnostics;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitSelectiveStashRepositoryTests
{
    [Fact]
    public async Task SelectiveStash_PreservesUnselectedTrackedAndUntrackedChanges()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuckettieSelectiveStashTests", Guid.NewGuid().ToString("N"));
        string repository = Path.Combine(root, "repository");
        CancellationToken token = TestContext.Current.CancellationToken;
        GitCommandClient client = new(TimeSpan.FromSeconds(10), Path.GetFullPath("Buckettie.AskPass.exe"), "developer");
        try
        {
            Directory.CreateDirectory(repository);
            await RunGitAsync(repository, token, "init", "--initial-branch=develop");
            await RunGitAsync(repository, token, "config", "user.name", "Test");
            await RunGitAsync(repository, token, "config", "user.email", "test@example.com");
            await File.WriteAllTextAsync(Path.Combine(repository, "a.txt"), "base a", token);
            await File.WriteAllTextAsync(Path.Combine(repository, "b.txt"), "base b", token);
            await RunGitAsync(repository, token, "add", "--", "a.txt", "b.txt");
            await RunGitAsync(repository, token, "commit", "-m", "base");
            await File.WriteAllTextAsync(Path.Combine(repository, "a.txt"), "changed a", token);
            await File.WriteAllTextAsync(Path.Combine(repository, "b.txt"), "changed b", token);
            await File.WriteAllTextAsync(Path.Combine(repository, "c.txt"), "new c", token);
            await File.WriteAllTextAsync(Path.Combine(repository, "d.txt"), "new d", token);

            GitCommandResult pushed = await client.PushStashAsync(repository, "selected files", ["a.txt", "c.txt"], token);
            pushed.IsSuccess.Should().BeTrue(pushed.StandardError);
            (await File.ReadAllTextAsync(Path.Combine(repository, "a.txt"), token)).Should().Be("base a");
            File.Exists(Path.Combine(repository, "c.txt")).Should().BeFalse();
            (await File.ReadAllTextAsync(Path.Combine(repository, "b.txt"), token)).Should().Be("changed b");
            File.Exists(Path.Combine(repository, "d.txt")).Should().BeTrue();

            GitCommandResult listed = await client.ListStashesAsync(repository, token);
            listed.IsSuccess.Should().BeTrue(listed.StandardError);
            string stashId = listed.StandardOutput.Split('\u001f', 2)[0];
            stashId.Should().HaveLength(40);
            GitCommandResult applied = await client.ApplyStashAsync(repository, stashId, token);
            applied.IsSuccess.Should().BeTrue(applied.StandardError);
            (await File.ReadAllTextAsync(Path.Combine(repository, "a.txt"), token)).Should().Be("changed a");
            (await File.ReadAllTextAsync(Path.Combine(repository, "c.txt"), token)).Should().Be("new c");
            (await File.ReadAllTextAsync(Path.Combine(repository, "b.txt"), token)).Should().Be("changed b");

            GitCommandResult dropped = await client.DropStashAsync(repository, "stash@{0}", token);
            dropped.IsSuccess.Should().BeTrue(dropped.StandardError);
            (await client.ListStashesAsync(repository, token)).StandardOutput.Should().BeEmpty();
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
