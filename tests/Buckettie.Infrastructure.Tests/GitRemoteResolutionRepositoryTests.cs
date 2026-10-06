using System.Diagnostics;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitRemoteResolutionRepositoryTests
{
    [Fact]
    public async Task Resolve_WithRealGitRemotes_SelectsHttpsRemoteAndReportsMissingRemotes()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuckettieRemoteResolutionTests", Guid.NewGuid().ToString("N"));
        CancellationToken token = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(root);
        try
        {
            await RunGitAsync(root, token, "init", "--initial-branch=develop");
            GitCommandClient client = new(TimeSpan.FromSeconds(10), Path.GetFullPath("Buckettie.AskPass.exe"), "developer");
            GitRemoteResolver resolver = new(client);

            (await resolver.ResolveAsync(root, "stk2k", "cupperpro", null, token)).Status
                .Should().Be(GitRemoteResolutionStatus.NotFound);

            await RunGitAsync(root, token, "remote", "add", "origin", "git@bitbucket.org:stk2k/cupperpro.git");
            await RunGitAsync(root, token, "remote", "add", "buckettie", "https://bitbucket.org/stk2k/cupperpro.git");
            GitRemoteResolution resolved = await resolver.ResolveAsync(root, "stk2k", "cupperpro", null, token);

            resolved.Status.Should().Be(GitRemoteResolutionStatus.Resolved);
            resolved.Name.Should().Be("buckettie");
            (await resolver.ResolveAsync(root, "stk2k", "cupperpro", "missing", token)).Status
                .Should().Be(GitRemoteResolutionStatus.NotFound);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task RunGitAsync(string workingDirectory, CancellationToken token, params string[] arguments)
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
        await process.StandardOutput.ReadToEndAsync(token);
        string error = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        process.ExitCode.Should().Be(0, error);
    }
}
