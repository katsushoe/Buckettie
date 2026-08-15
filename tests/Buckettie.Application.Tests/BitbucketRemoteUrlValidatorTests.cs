using Buckettie.Application.Repositories;
using FluentAssertions;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class BitbucketRemoteUrlValidatorTests
{
    private readonly BitbucketRemoteUrlValidator _validator = new();

    [Theory]
    [InlineData("https://bitbucket.org/example-workspace/buckettie.git")]
    [InlineData("git@bitbucket.org:example-workspace/buckettie.git")]
    [InlineData("ssh://git@bitbucket.org/example-workspace/buckettie.git")]
    public void Validate_WhenRemoteMatchesConfiguredRepository_ReturnsValid(string remoteUrl)
    {
        RepositoryValidationResult result = _validator.Validate("example-workspace", "buckettie", remoteUrl);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com/example-workspace/buckettie.git")]
    [InlineData("https://token@bitbucket.org/example-workspace/buckettie.git")]
    [InlineData("file:///example-workspace/buckettie.git")]
    [InlineData("https://bitbucket.org/example-workspace/buckettie.git?token=secret")]
    [InlineData("not-a-url")]
    public void Validate_WhenRemoteFormatIsNotAllowed_ReturnsInvalidUrl(string remoteUrl)
    {
        RepositoryValidationResult result = _validator.Validate("example-workspace", "buckettie", remoteUrl);

        result.Error.Should().Be(RepositoryValidationError.RemoteUrlInvalid);
    }

    [Fact]
    public void Validate_WhenRemoteTargetsAnotherRepository_ReturnsMismatch()
    {
        RepositoryValidationResult result = _validator.Validate(
            "example-workspace",
            "buckettie",
            "https://bitbucket.org/example-workspace/another.git");

        result.Error.Should().Be(RepositoryValidationError.RemoteMismatch);
    }
}
