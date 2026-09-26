using System.Diagnostics;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitCommitAuthorRepositoryTests
{
    [Fact]
    public async Task Commit_WithoutAnyGitIdentity_SucceedsWithRegisteredAuthor()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuckettieCommitAuthorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string emptyGlobal = Path.Combine(root, "empty-global.gitconfig");
        await File.WriteAllTextAsync(emptyGlobal, string.Empty, TestContext.Current.CancellationToken);
        string? previousGlobal = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        string? previousNoSystem = Environment.GetEnvironmentVariable("GIT_CONFIG_NOSYSTEM");
        // Models the LocalSystem service: no global, system, or repository identity is available.
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", emptyGlobal);
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
        try
        {
            CancellationToken token = TestContext.Current.CancellationToken;
            string repository = Path.Combine(root, "repository");
            Directory.CreateDirectory(repository);
            await RunGitAsync(repository, token, "init", "--initial-branch=develop");
            await File.WriteAllTextAsync(Path.Combine(repository, "content.txt"), "content\n", token);
            GitCommandClient client = new(TimeSpan.FromSeconds(10), Path.GetFullPath("Buckettie.AskPass.exe"), "developer");

            (await client.GetConfigValueAsync(repository, "user.name", token)).IsSuccess.Should().BeFalse();
            (await client.GetConfigValueAsync(repository, "user.email", token)).IsSuccess.Should().BeFalse();
            (await client.StageAllAsync(repository, token)).IsSuccess.Should().BeTrue();
            GitCommandResult commit = await client.CommitAsync(repository, "feat: add content",
                new GitCommitAuthor("Registered Author", "registered@example.com"), token);

            commit.IsSuccess.Should().BeTrue(commit.StandardError);
            (await RunGitAsync(repository, token, "log", "-1", "--format=%an|%ae|%cn|%ce")).Trim()
                .Should().Be("Registered Author|registered@example.com|Registered Author|registered@example.com");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", previousGlobal);
            Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", previousNoSystem);
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
        string output = await process.StandardOutput.ReadToEndAsync(token);
        string error = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        process.ExitCode.Should().Be(0, error);
        return output;
    }
}
