using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class RemoteReferenceIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"buckettie-refs-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetRemoteHeadAsync_WithRealGit_DistinguishesMissingFromExistingWithoutChangingHead()
    {
        string metadata = Path.Combine(_root, ".git");
        Directory.CreateDirectory(Path.Combine(metadata, "objects"));
        Directory.CreateDirectory(Path.Combine(metadata, "refs", "heads"));
        Directory.CreateDirectory(Path.Combine(metadata, "refs", "remotes", "origin"));
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        string headPath = Path.Combine(metadata, "HEAD");
        await File.WriteAllTextAsync(headPath, "ref: refs/heads/main\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(metadata, "refs", "heads", "main"), hash + "\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(metadata, "refs", "remotes", "origin", "main"), hash + "\n", TestContext.Current.CancellationToken);
        GitCommandClient client = new(TimeSpan.FromSeconds(10), Path.Combine(_root, "askpass.exe"), "test-user");

        GitCommandResult missing = await client.GetRemoteHeadAsync(_root, "origin", "develop", TestContext.Current.CancellationToken);
        GitCommandResult existing = await client.GetRemoteHeadAsync(_root, "origin", "main", TestContext.Current.CancellationToken);

        missing.Failure.Should().Be(GitCommandFailure.ReferenceNotFound);
        existing.IsSuccess.Should().BeTrue();
        existing.StandardOutput.Trim().Should().Be(hash);
        (await File.ReadAllTextAsync(headPath, TestContext.Current.CancellationToken)).Should().Be("ref: refs/heads/main\n");
        File.Exists(Path.Combine(metadata, "refs", "heads", "develop")).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
