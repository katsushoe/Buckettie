using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Buckettie.Application.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class GitGatewayTests
{
    private const string RepositoryRoot = "C:\\Repositories\\Buckettie";
    private readonly IGitCommandClient _git = Substitute.For<IGitCommandClient>();
    private readonly IRepositoryEnvironment _environment = Substitute.For<IRepositoryEnvironment>();

    [Fact]
    public async Task GetStatusAsync_WhenRepositoryIsValid_ReturnsStatus()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("develop\n"));
        _git.GetHeadAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("abc123\n"));
        _git.GetStatusAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success());
        _git.GetRemoteHeadAsync(RepositoryRoot, "origin", "develop", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("def456\n"));
        _git.GetRemoteHeadAsync(RepositoryRoot, "origin", "main", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("fed987\n"));
        _git.GetAheadBehindAsync(RepositoryRoot, "origin", "develop", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("2\t3\n"));

        GitGatewayResult result = await gateway.GetStatusAsync(
            "buckettie",
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(new GitRepositoryStatus(
            "buckettie", "develop", "abc123", "def456", "fed987", 2, 3, true));
    }

    [Fact]
    public async Task GetStatusAsync_WhenDivergenceOutputIsInvalid_ReturnsGitFailed()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("develop"));
        _git.GetHeadAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("abc123"));
        _git.GetStatusAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success());
        _git.GetRemoteHeadAsync(RepositoryRoot, "origin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("def456"));
        _git.GetAheadBehindAsync(RepositoryRoot, "origin", "develop", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("invalid"));

        GitGatewayResult result = await gateway.GetStatusAsync(
            "buckettie",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.GitFailed);
    }

    [Fact]
    public async Task GetStatusAsync_WhenRepositoryIdIsInvalid_ReturnsNotAllowed()
    {
        GitGateway gateway = CreateGateway();

        GitGatewayResult result = await gateway.GetStatusAsync(
            "../repository",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.RepositoryNotAllowed);
    }

    [Fact]
    public async Task FetchAsync_WhenRemoteDoesNotMatch_DoesNotFetch()
    {
        GitGateway gateway = CreateGateway();
        ConfigureLocalPath();
        _git.GetRemoteUrlAsync(RepositoryRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/example/another.git"));

        GitGatewayResult result = await gateway.FetchAsync(
            "buckettie",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.RemoteMismatch);
        await _git.DidNotReceiveWithAnyArgs().FetchAsync(
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FetchAsync_WhenRemoteUsesSsh_ReturnsDedicatedErrorWithoutFetching()
    {
        GitGateway gateway = CreateGateway();
        ConfigureLocalPath();
        _git.GetRemoteUrlAsync(RepositoryRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("git@bitbucket.org:example/buckettie.git"));

        GitGatewayResult result = await gateway.FetchAsync(
            "buckettie",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.SshRemoteNotSupported);
        await _git.DidNotReceiveWithAnyArgs().FetchAsync(
            default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PullAsync_WhenBranchIsAllowed_UsesFastForwardOnlyBoundary()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("develop"));
        _git.PullFastForwardOnlyAsync(
                RepositoryRoot,
                "origin",
                "develop",
                "buckettie",
                Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success());

        GitGatewayResult result = await gateway.PullAsync(
            "buckettie",
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PushAsync_WhenCurrentBranchIsMain_ReturnsProtectedBranch()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("main"));
        _git.GetStatusAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success());

        GitGatewayResult result = await gateway.PushAsync(
            "buckettie",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.ProtectedBranch);
        await _git.DidNotReceiveWithAnyArgs().PushAsync(
            default!,
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PushAsync_WhenWorkingTreeIsDirty_ReturnsWorkingTreeDirty()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("develop"));
        _git.GetStatusAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success(" M file.cs"));

        GitGatewayResult result = await gateway.PushAsync(
            "buckettie",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.WorkingTreeDirty);
    }

    private GitGateway CreateGateway()
    {
        BuckettieOptions options = new()
        {
            AtlassianEmail = "developer@example.com",
            BitbucketUsername = "developer",
            Repositories = new Dictionary<string, RepositoryOptions>
            {
                ["buckettie"] = CreateRepository(),
            },
        };
        return new(
            new RepositoryAllowlist(options),
            new LocalPathValidator(_environment),
            new BitbucketRemoteUrlValidator(),
            _git);
    }

    private void ConfigureBoundary()
    {
        ConfigureLocalPath();
        _git.GetRemoteUrlAsync(RepositoryRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/example/buckettie.git"));
    }

    private void ConfigureLocalPath()
    {
        _environment.GetFullPath(RepositoryRoot).Returns(RepositoryRoot);
        _environment.DirectoryExists(RepositoryRoot).Returns(true);
        _environment.ContainsReparsePoint(RepositoryRoot).Returns(false);
        _environment.GitMetadataExists(RepositoryRoot).Returns(true);
    }

    private static RepositoryOptions CreateRepository() => new()
    {
        Workspace = "example",
        Slug = "buckettie",
        LocalRoot = RepositoryRoot,
        Remote = "origin",
        DevelopBranch = "develop",
        MainBranch = "main",
        DirectPushBranches = new HashSet<string> { "develop" },
        PullBranches = new HashSet<string> { "develop", "main" },
        ProtectedBranches = new HashSet<string> { "main" },
        TagTargetBranch = "main",
        TagPattern = "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
        RequireCleanWorkingTree = true,
    };
}
