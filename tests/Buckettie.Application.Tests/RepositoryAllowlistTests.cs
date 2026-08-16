using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using FluentAssertions;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class RepositoryAllowlistTests
{
    [Fact]
    public void TryGet_WhenRepositoryIsConfigured_ReturnsSettings()
    {
        RepositoryOptions repository = CreateRepository();
        BuckettieOptions options = new()
        {
            AtlassianEmail = "developer@example.com",
            BitbucketUsername = "developer",
            Repositories = new Dictionary<string, RepositoryOptions> { ["buckettie"] = repository },
        };
        RepositoryAllowlist allowlist = new(options);

        bool found = allowlist.TryGet("buckettie", out RepositoryOptions? actual);

        found.Should().BeTrue();
        actual.Should().BeSameAs(repository);
    }

    [Fact]
    public void TryGet_WhenRepositoryIsNotConfigured_ReturnsFalse()
    {
        BuckettieOptions options = new()
        {
            AtlassianEmail = "developer@example.com",
            BitbucketUsername = "developer",
            Repositories = new Dictionary<string, RepositoryOptions>(),
        };
        RepositoryAllowlist allowlist = new(options);

        bool found = allowlist.TryGet("unknown", out RepositoryOptions? actual);

        found.Should().BeFalse();
        actual.Should().BeNull();
    }

    private static RepositoryOptions CreateRepository() => new()
    {
        Workspace = "example-workspace",
        Slug = "buckettie",
        LocalRoot = "repository-root",
        Remote = "origin",
        DevelopBranch = "develop",
        MainBranch = "main",
        DirectPushBranches = new HashSet<string> { "develop" },
        PullBranches = new HashSet<string> { "develop", "main" },
        ProtectedBranches = new HashSet<string> { "main" },
        TagTargetBranch = "main",
        TagPattern = "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
    };
}
