using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Buckettie.Application.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class RepositoryRegistrationValidatorTests
{
    private const string LocalRoot = "C:\\Repositories\\NewRepo";
    private readonly IRepositoryEnvironment _environment = Substitute.For<IRepositoryEnvironment>();
    private readonly IGitCommandClient _git = Substitute.For<IGitCommandClient>();

    public RepositoryRegistrationValidatorTests()
    {
        _environment.GetFullPath(Arg.Any<string>()).Returns(callInfo => (string)callInfo[0]);
    }

    [Fact]
    public async Task ValidateAsync_WhenEverythingIsValid_ReturnsDerivedCoordinates()
    {
        RepositoryRegistrationValidator validator = CreateValidator();
        ConfigureValidLocalRoot();
        _git.GetRemoteUrlAsync(LocalRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/example-workspace/new-repo.git\n"));

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
        result.Workspace.Should().Be("example-workspace");
        result.Slug.Should().Be("new-repo");
        result.LocalRoot.Should().Be(LocalRoot);
    }

    [Fact]
    public async Task ValidateAsync_WhenRemoteUsesBitbucketSsh_ReturnsDedicatedError()
    {
        RepositoryRegistrationValidator validator = CreateValidator();
        ConfigureValidLocalRoot();
        _git.GetRemoteUrlAsync(LocalRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("git@bitbucket.org:example-workspace/new-repo.git\n"));

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(RepositoryValidationError.SshRemoteNotSupported);
    }

    [Fact]
    public async Task ValidateAsync_WhenRepositoryIdIsInvalid_ReturnsRepositoryIdInvalid()
    {
        RepositoryRegistrationValidator validator = CreateValidator();

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "../escape", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(RepositoryValidationError.RepositoryIdInvalid);
    }

    [Theory]
    [InlineData("Buckettie")]
    [InlineData("ai_prompt")]
    [InlineData("obsidian-vault")]
    public async Task ValidateAsync_WhenRepositoryIdViolatesProjectInboxRule_ReturnsRepositoryIdInvalid(
        string repositoryId)
    {
        RepositoryRegistrationValidator validator = CreateValidator();

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            repositoryId, LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.RepositoryIdInvalid);
    }

    [Fact]
    public async Task ValidateAsync_WhenRepositoryIdAlreadyRegistered_ReturnsAlreadyRegistered()
    {
        RepositoryRegistrationValidator validator = CreateValidator(existingRepositoryId: "buckettie");

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "buckettie", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.RepositoryAlreadyRegistered);
    }

    [Fact]
    public async Task ValidateAsync_WhenLocalRootDoesNotExist_ReturnsLocalRootNotFound()
    {
        RepositoryRegistrationValidator validator = CreateValidator();
        _environment.DirectoryExists(LocalRoot).Returns(false);

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.LocalRootNotFound);
    }

    [Fact]
    public async Task ValidateAsync_WhenLocalRootContainsReparsePoint_ReturnsReparsePoint()
    {
        RepositoryRegistrationValidator validator = CreateValidator();
        _environment.DirectoryExists(LocalRoot).Returns(true);
        _environment.ContainsReparsePoint(LocalRoot).Returns(true);

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.LocalPathReparsePoint);
    }

    [Fact]
    public async Task ValidateAsync_WhenGitMetadataIsMissing_ReturnsGitMetadataNotFound()
    {
        RepositoryRegistrationValidator validator = CreateValidator();
        _environment.DirectoryExists(LocalRoot).Returns(true);
        _environment.ContainsReparsePoint(LocalRoot).Returns(false);
        _environment.GitMetadataExists(LocalRoot).Returns(false);

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.GitMetadataNotFound);
    }

    [Fact]
    public async Task ValidateAsync_WhenRemoteCommandFails_ReturnsRemoteUrlInvalid()
    {
        RepositoryRegistrationValidator validator = CreateValidator();
        ConfigureValidLocalRoot();
        _git.GetRemoteUrlAsync(LocalRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Failed(GitCommandFailure.Failed, "fatal: no such remote"));

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.RemoteUrlInvalid);
    }

    [Fact]
    public async Task ValidateAsync_WhenRemoteUrlIsNotBitbucket_ReturnsRemoteUrlInvalid()
    {
        RepositoryRegistrationValidator validator = CreateValidator();
        ConfigureValidLocalRoot();
        _git.GetRemoteUrlAsync(LocalRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://example.com/example-workspace/new-repo.git\n"));

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.RemoteUrlInvalid);
    }

    [Fact]
    public async Task ValidateAsync_WhenRemoteMatchesAnExistingRepository_ReturnsAlreadyRegistered()
    {
        RepositoryRegistrationValidator validator = CreateValidator(
            existingRepositoryId: "buckettie",
            existingWorkspace: "example-workspace",
            existingSlug: "new-repo",
            existingLocalRoot: "C:\\Repositories\\Other");
        ConfigureValidLocalRoot();
        _git.GetRemoteUrlAsync(LocalRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/example-workspace/new-repo.git\n"));

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.RepositoryAlreadyRegistered);
    }

    [Fact]
    public async Task ValidateAsync_WhenLocalRootMatchesAnExistingRepository_ReturnsAlreadyRegistered()
    {
        RepositoryRegistrationValidator validator = CreateValidator(
            existingRepositoryId: "buckettie",
            existingWorkspace: "example-workspace",
            existingSlug: "another-repo",
            existingLocalRoot: LocalRoot);
        ConfigureValidLocalRoot();
        _git.GetRemoteUrlAsync(LocalRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/example-workspace/new-repo.git\n"));

        RepositoryRegistrationValidationResult result = await validator.ValidateAsync(
            "newrepo", LocalRoot, "origin", TestContext.Current.CancellationToken);

        result.Error.Should().Be(RepositoryValidationError.RepositoryAlreadyRegistered);
    }

    private void ConfigureValidLocalRoot()
    {
        _environment.DirectoryExists(LocalRoot).Returns(true);
        _environment.ContainsReparsePoint(LocalRoot).Returns(false);
        _environment.GitMetadataExists(LocalRoot).Returns(true);
    }

    private RepositoryRegistrationValidator CreateValidator(
        string? existingRepositoryId = null,
        string existingWorkspace = "example-workspace",
        string existingSlug = "buckettie",
        string existingLocalRoot = "C:\\Repositories\\Buckettie")
    {
        Dictionary<string, RepositoryOptions> repositories = new(StringComparer.OrdinalIgnoreCase);
        if (existingRepositoryId is not null)
        {
            repositories[existingRepositoryId] = new RepositoryOptions
            {
                Workspace = existingWorkspace,
                Slug = existingSlug,
                LocalRoot = existingLocalRoot,
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

        BuckettieOptions options = new()
        {
            AtlassianEmail = "developer@example.com",
            BitbucketUsername = "developer",
            Repositories = repositories,
        };
        RepositoryAllowlist allowlist = new(options);
        return new RepositoryRegistrationValidator(allowlist, _environment, _git);
    }
}
