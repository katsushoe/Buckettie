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

        result.Status.Should().BeEquivalentTo(new GitRepositoryStatus(
            "buckettie", "develop", "abc123", "def456", "fed987", 2, 3, true,
            "refs/remotes/origin/develop", null, []));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GetStatusAsync_WhenRemoteReferencesAreMissing_ReturnsLocalStatus(bool missingDevelop, bool missingMain)
    {
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>()).Returns(GitCommandResult.Success("main"));
        _git.GetHeadAsync(RepositoryRoot, Arg.Any<CancellationToken>()).Returns(GitCommandResult.Success("abc123"));
        _git.GetStatusAsync(RepositoryRoot, Arg.Any<CancellationToken>()).Returns(GitCommandResult.Success(" M file.txt"));
        _git.GetRemoteHeadAsync(RepositoryRoot, "origin", "develop", Arg.Any<CancellationToken>())
            .Returns(missingDevelop ? GitCommandResult.Failed(GitCommandFailure.ReferenceNotFound) : GitCommandResult.Success("def456"));
        _git.GetRemoteHeadAsync(RepositoryRoot, "origin", "main", Arg.Any<CancellationToken>())
            .Returns(missingMain ? GitCommandResult.Failed(GitCommandFailure.ReferenceNotFound) : GitCommandResult.Success("abc123"));
        _git.GetAheadBehindAsync(RepositoryRoot, "origin", "develop", Arg.Any<CancellationToken>()).Returns(GitCommandResult.Success("1\t2"));

        GitGatewayResult result = await CreateGateway().GetStatusAsync("buckettie", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Status!.LocalBranch.Should().Be("main");
        result.Status.LocalHead.Should().Be("abc123");
        result.Status.WorkingTreeClean.Should().BeFalse();
        result.Status.RemoteDevelopHead.Should().Be(missingDevelop ? null : "def456");
        result.Status.RemoteMainHead.Should().Be(missingMain ? null : "abc123");
        result.Status.Ahead.Should().Be(missingDevelop ? null : 1);
        result.Status.Behind.Should().Be(missingDevelop ? null : 2);
        result.Status.ComparisonReference.Should().Be("refs/remotes/origin/develop");
        result.Status.ComparisonUnavailableReason.Should().Be(missingDevelop ? "remote_tracking_ref_missing_or_not_fetched" : null);
        result.Status.MissingRemoteReferences.Should().HaveCount((missingDevelop ? 1 : 0) + (missingMain ? 1 : 0));
        await _git.Received(missingDevelop ? 0 : 1).GetAheadBehindAsync(RepositoryRoot, "origin", "develop", Arg.Any<CancellationToken>());
        await _git.DidNotReceiveWithAnyArgs().FetchAsync(default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(GitCommandFailure.Failed, "Permission denied", GitGatewayError.PermissionDenied)]
    [InlineData(GitCommandFailure.Failed, "fatal: broken repository", GitGatewayError.GitFailed)]
    [InlineData(GitCommandFailure.TimedOut, "", GitGatewayError.Timeout)]
    [InlineData(GitCommandFailure.Cancelled, "", GitGatewayError.Cancelled)]
    public async Task GetStatusAsync_WhenReferenceReadFails_DoesNotHideFailure(GitCommandFailure failure, string message, GitGatewayError expected)
    {
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>()).Returns(GitCommandResult.Success("main"));
        _git.GetHeadAsync(RepositoryRoot, Arg.Any<CancellationToken>()).Returns(GitCommandResult.Success("abc123"));
        _git.GetStatusAsync(RepositoryRoot, Arg.Any<CancellationToken>()).Returns(GitCommandResult.Success());
        _git.GetRemoteHeadAsync(RepositoryRoot, "origin", "develop", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Failed(failure, message));

        GitGatewayResult result = await CreateGateway().GetStatusAsync("buckettie", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(expected);
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
    public async Task GetDiffAsync_WhenRepositoryIsValid_ReturnsWorkingTreeDiff()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetDiffAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("diff --git a/file.cs b/file.cs\n"));

        GitGatewayResult result = await gateway.GetDiffAsync(
            "buckettie", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Diff.Should().Be("diff --git a/file.cs b/file.cs\n");
    }

    [Fact]
    public async Task GetDiffAsync_WhenRepositoryIsNotRegistered_ReturnsNotAllowed()
    {
        GitGateway gateway = CreateGateway();

        GitGatewayResult result = await gateway.GetDiffAsync(
            "unknown", TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.RepositoryNotAllowed);
    }

    [Fact]
    public async Task CommitAsync_WhenBranchIsAllowed_CommitsAllChangesAndReturnsHead()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("develop\n"));
        _git.GetStatusAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success(" M file.cs\n"));
        _git.StageAllAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success());
        _git.CommitAsync(RepositoryRoot, "feat: change", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success());
        _git.GetHeadAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("abc123\n"));

        GitGatewayResult result = await gateway.CommitAsync(
            "buckettie", "feat: change", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Branch.Should().Be("develop");
        result.CommitHash.Should().Be("abc123");
    }

    [Fact]
    public async Task CommitAsync_WhenCurrentBranchIsProtected_ReturnsProtectedBranch()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("main"));

        GitGatewayResult result = await gateway.CommitAsync(
            "buckettie", "feat: change", TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.ProtectedBranch);
        await _git.DidNotReceiveWithAnyArgs().StageAllAsync(
            default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CommitAsync_WhenRepositoryIsNotRegistered_ReturnsNotAllowed()
    {
        GitGateway gateway = CreateGateway();

        GitGatewayResult result = await gateway.CommitAsync(
            "unknown", "feat: change", TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.RepositoryNotAllowed);
    }

    [Fact]
    public async Task CommitAsync_WhenWorkingTreeIsClean_ReturnsNothingToCommit()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.GetCurrentBranchAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("develop"));
        _git.GetStatusAsync(RepositoryRoot, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success());

        GitGatewayResult result = await gateway.CommitAsync(
            "buckettie", "feat: change", TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.NothingToCommit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CommitAsync_WhenMessageIsInvalid_ReturnsCommitMessageInvalid(string message)
    {
        GitGateway gateway = CreateGateway();

        GitGatewayResult result = await gateway.CommitAsync(
            "buckettie", message, TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.InvalidCommitMessage);
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

    [Fact]
    public async Task PushTagAsync_WhenTagIsValid_PushesExactTagReference()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.PushTagAsync(
                RepositoryRoot, "origin", "v1.2.3", "buckettie", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success());

        GitGatewayResult result = await gateway.PushTagAsync(
            "buckettie", "v1.2.3", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PushTagAsync_WhenLocalTagDoesNotExist_ReturnsReferenceNotFound()
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.PushTagAsync(
                RepositoryRoot, "origin", "v1.2.3", "buckettie", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Failed(
                GitCommandFailure.Failed, "error: src refspec refs/tags/v1.2.3 does not match any"));

        GitGatewayResult result = await gateway.PushTagAsync(
            "buckettie", "v1.2.3", TestContext.Current.CancellationToken);

        result.Error.Should().Be(GitGatewayError.ReferenceNotFound);
    }

    [Theory]
    [InlineData("fatal: Authentication failed for 'https://user:secret@bitbucket.org/work/repo.git/'", GitGatewayError.AuthenticationFailed)]
    [InlineData("fatal: unable to access 'https://bitbucket.org/work/repo.git/': Could not resolve host", GitGatewayError.NetworkError)]
    [InlineData("remote: Permission denied to repository", GitGatewayError.PermissionDenied)]
    [InlineData("CONFLICT (content): Merge conflict in file.cs", GitGatewayError.Conflict)]
    [InlineData("fatal: Not possible to fast-forward, aborting. non-fast-forward", GitGatewayError.NonFastForward)]
    [InlineData("fatal: unknown failure at C:\\Users\\person\\repo", GitGatewayError.GitFailed)]
    public async Task FetchAsync_WhenGitFails_ClassifiesFailureWithoutExposingStandardError(
        string standardError,
        GitGatewayError expected)
    {
        GitGateway gateway = CreateGateway();
        ConfigureBoundary();
        _git.FetchAsync(RepositoryRoot, "origin", "buckettie", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Failed(GitCommandFailure.Failed, standardError));

        GitGatewayResult result = await gateway.FetchAsync(
            "buckettie",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(expected);
        result.CorrelationId.Should().MatchRegex("^[0-9a-f]{32}$");
        result.ToString().Should().NotContain("secret").And.NotContain("C:\\Users");
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
