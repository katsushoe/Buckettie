using Buckettie.Application.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class LocalPathValidatorTests
{
    private const string AllowedPath = "C:\\Repositories\\Buckettie";
    private readonly IRepositoryEnvironment _environment = Substitute.For<IRepositoryEnvironment>();

    [Fact]
    public void Validate_WhenPathIsConfiguredGitRepository_ReturnsValid()
    {
        ConfigureSafeRepository();
        LocalPathValidator validator = new(_environment);

        RepositoryValidationResult result = validator.Validate(AllowedPath, AllowedPath);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenCandidateEscapesConfiguredRoot_ReturnsPathMismatch()
    {
        ConfigureSafeRepository();
        _environment.GetFullPath("C:\\Repositories\\Buckettie\\..").Returns("C:\\Repositories");
        LocalPathValidator validator = new(_environment);

        RepositoryValidationResult result = validator.Validate(AllowedPath, "C:\\Repositories\\Buckettie\\..");

        result.Error.Should().Be(RepositoryValidationError.LocalPathMismatch);
    }

    [Fact]
    public void Validate_WhenRootIsReparsePoint_ReturnsReparsePointError()
    {
        ConfigureSafeRepository();
        _environment.ContainsReparsePoint(AllowedPath).Returns(true);
        LocalPathValidator validator = new(_environment);

        RepositoryValidationResult result = validator.Validate(AllowedPath, AllowedPath);

        result.Error.Should().Be(RepositoryValidationError.LocalPathReparsePoint);
    }

    [Fact]
    public void Validate_WhenGitMetadataIsMissing_ReturnsGitMetadataError()
    {
        ConfigureSafeRepository();
        _environment.GitMetadataExists(AllowedPath).Returns(false);
        LocalPathValidator validator = new(_environment);

        RepositoryValidationResult result = validator.Validate(AllowedPath, AllowedPath);

        result.Error.Should().Be(RepositoryValidationError.GitMetadataNotFound);
    }

    private void ConfigureSafeRepository()
    {
        _environment.GetFullPath(AllowedPath).Returns(AllowedPath);
        _environment.DirectoryExists(AllowedPath).Returns(true);
        _environment.GitMetadataExists(AllowedPath).Returns(true);
        _environment.ContainsReparsePoint(AllowedPath).Returns(false);
    }
}
