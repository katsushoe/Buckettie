using System.Diagnostics;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitHistoryRewriteRepositoryTests
{
    [Fact]
    public async Task RewriteLatestCommit_InDisposableRepository_PreservesContentAndDates()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuckettieHistoryRewriteTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CancellationToken token = TestContext.Current.CancellationToken;
            await RunGitAsync(root, token, "init", "--initial-branch=develop");
            await File.WriteAllTextAsync(Path.Combine(root, "content.txt"), "content\n", token);
            await RunGitAsync(root, token, "add", "content.txt");
            Dictionary<string, string> originalEnvironment = new()
            {
                ["GIT_AUTHOR_NAME"] = "Old Author",
                ["GIT_AUTHOR_EMAIL"] = "old-author@example.com",
                ["GIT_AUTHOR_DATE"] = "2026-09-01T10:00:00+09:00",
                ["GIT_COMMITTER_NAME"] = "Old Committer",
                ["GIT_COMMITTER_EMAIL"] = "old-committer@example.com",
                ["GIT_COMMITTER_DATE"] = "2026-09-01T10:01:00+09:00",
            };
            await RunGitAsync(root, token, originalEnvironment, "commit", "-m", "subject\n\nbody");
            string remoteRoot = Path.Combine(root, "remote.git");
            Directory.CreateDirectory(remoteRoot);
            await RunGitAsync(remoteRoot, token, "init", "--bare");
            await RunGitAsync(root, token, "remote", "add", "origin", remoteRoot);
            await RunGitAsync(root, token, "push", "origin", "develop");

            GitCommandClient client = new(TimeSpan.FromSeconds(10), Path.GetFullPath("Buckettie.AskPass.exe"),
                "developer");
            string oldHead = (await client.GetHeadAsync(root, token)).StandardOutput.Trim();
            string[] before = (await client.GetCommitMetadataAsync(root, oldHead, token)).StandardOutput.Split('\u001f', 11);
            Dictionary<string, string> rewrittenEnvironment = new(originalEnvironment)
            {
                ["GIT_AUTHOR_EMAIL"] = "new-author@example.com",
            };

            string recovery = $"refs/buckettie/recovery/develop/{oldHead}";
            (await client.CreateReferenceAsync(root, recovery, oldHead, token)).IsSuccess.Should().BeTrue();
            var created = await client.CreateCommitAsync(
                root, $"{before[1]}\u001f{before[2]}\u001f{before[10]}", rewrittenEnvironment, token);
            created.IsSuccess.Should().BeTrue(created.StandardError);
            string newHead = created.StandardOutput.Trim();
            (await client.UpdateBranchReferenceAsync(root, "develop", newHead, oldHead, token))
                .IsSuccess.Should().BeTrue();
            string[] after = (await client.GetCommitMetadataAsync(root, newHead, token)).StandardOutput.Split('\u001f', 11);

            after[1].Should().Be(before[1]);
            after[2].Should().Be(before[2]);
            after[5].Should().Be(before[5]);
            after[8].Should().Be(before[8]);
            after[10].Should().Be(before[10]);
            after[4].Should().Be("new-author@example.com");
            (await RunGitAsync(root, token, "rev-parse", recovery)).Trim().Should().Be(oldHead);
            string actualBefore = (await client.GetActualRemoteHeadAsync(root, "origin", "develop", "test", token))
                .StandardOutput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
            actualBefore.Should().Be(oldHead);
            (await client.ForcePushWithLeaseAsync(root, "origin", "develop", oldHead, "test", token))
                .IsSuccess.Should().BeTrue();
            string actualAfter = (await client.GetActualRemoteHeadAsync(root, "origin", "develop", "test", token))
                .StandardOutput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
            actualAfter.Should().Be(newHead);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static Task<string> RunGitAsync(
        string root, CancellationToken token, params string[] arguments) =>
        RunGitAsync(root, token, new Dictionary<string, string>(), arguments);

    private static async Task<string> RunGitAsync(
        string root, CancellationToken token, IReadOnlyDictionary<string, string> environment,
        params string[] arguments)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        foreach ((string name, string value) in environment) start.Environment[name] = value;
        using Process process = Process.Start(start)!;
        string output = await process.StandardOutput.ReadToEndAsync(token);
        string error = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        process.ExitCode.Should().Be(0, error);
        return output;
    }
}
