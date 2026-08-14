using Buckettie.Domain;
using FluentAssertions;
using Xunit;

namespace Buckettie.Domain.Tests;

public sealed class RepositoryPolicyTests
{
    private readonly RepositoryPolicy _policy = new(
        "buckettie",
        "develop",
        "main",
        new HashSet<string>(StringComparer.Ordinal) { "develop" },
        new HashSet<string>(StringComparer.Ordinal) { "develop", "main" },
        new HashSet<PullRequestRoute> { new("develop", "main") },
        new HashSet<string>(StringComparer.Ordinal) { "main" },
        "main",
        "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
        true);

    [Fact]
    public void ValidatePush_WhenDevelopAndClean_ReturnsAllowed()
    {
        PolicyResult result = _policy.ValidatePush("develop", true);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidatePush_WhenMain_ReturnsProtectedBranch()
    {
        PolicyResult result = _policy.ValidatePush("main", true);

        result.ErrorCode.Should().Be(PolicyErrorCode.ProtectedBranch);
    }

    [Fact]
    public void ValidatePullRequest_WhenDevelopToMain_ReturnsAllowed()
    {
        PolicyResult result = _policy.ValidatePullRequest("develop", "main");

        result.IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("v1.0.0", "main", true)]
    [InlineData("release-1", "main", false)]
    [InlineData("v1.0.0", "develop", false)]
    public void ValidateTag_WhenCalled_ReturnsExpectedResult(string tag, string branch, bool expected)
    {
        PolicyResult result = _policy.ValidateTag(tag, branch);

        result.IsAllowed.Should().Be(expected);
    }
}
