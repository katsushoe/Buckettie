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

    [Fact]
    public void Register_WhenRepositoryIdIsNew_AddsItAndReadersSeeIt()
    {
        RepositoryAllowlist allowlist = new(CreateOptions());
        RepositoryOptions repository = CreateRepository();

        bool registered = allowlist.Register("new-repo", repository);

        registered.Should().BeTrue();
        allowlist.TryGet("new-repo", out RepositoryOptions? actual).Should().BeTrue();
        actual.Should().BeSameAs(repository);
        allowlist.TryGet("buckettie", out _).Should().BeTrue();
    }

    [Fact]
    public void Register_WhenRepositoryIdAlreadyExists_ReturnsFalseAndDoesNotReplace()
    {
        RepositoryOptions original = CreateRepository();
        BuckettieOptions options = CreateOptions(original);
        RepositoryAllowlist allowlist = new(options);

        bool registered = allowlist.Register("buckettie", CreateRepository() with { Slug = "different" });

        registered.Should().BeFalse();
        allowlist.TryGet("buckettie", out RepositoryOptions? actual);
        actual.Should().BeSameAs(original);
    }

    [Fact]
    public void Snapshot_ReflectsRegisteredRepositories()
    {
        RepositoryAllowlist allowlist = new(CreateOptions());

        allowlist.Register("new-repo", CreateRepository());

        allowlist.Snapshot().Keys.Should().Contain(["buckettie", "new-repo"]);
    }

    private static BuckettieOptions CreateOptions(RepositoryOptions? repository = null) => new()
    {
        AtlassianEmail = "developer@example.com",
        BitbucketUsername = "developer",
        Repositories = new Dictionary<string, RepositoryOptions> { ["buckettie"] = repository ?? CreateRepository() },
    };

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
