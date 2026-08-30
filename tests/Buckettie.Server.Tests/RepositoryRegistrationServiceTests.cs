using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Buckettie.Application.Credentials;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class RepositoryRegistrationServiceTests
{
    private const string LocalRoot = "C:\\Repositories\\NewRepo";
    private readonly IRepositoryEnvironment _environment = Substitute.For<IRepositoryEnvironment>();
    private readonly IGitCommandClient _git = Substitute.For<IGitCommandClient>();
    private readonly IInteractiveApprovalPrompt _approvalPrompt = Substitute.For<IInteractiveApprovalPrompt>();
    private readonly IApiTokenStore _tokenStore = Substitute.For<IApiTokenStore>();

    public RepositoryRegistrationServiceTests()
    {
        _environment.GetFullPath(Arg.Any<string>()).Returns(callInfo => (string)callInfo[0]);
        _environment.DirectoryExists(LocalRoot).Returns(true);
        _environment.ContainsReparsePoint(LocalRoot).Returns(false);
        _environment.GitMetadataExists(LocalRoot).Returns(true);
        _git.GetRemoteUrlAsync(LocalRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/example-workspace/new-repo.git\n"));
        _tokenStore.Read(Arg.Any<string>()).Returns(ApiTokenStoreResult.Success("existing-token"));
    }

    [Fact]
    public async Task RegisterAsync_WhenValidationFails_NeverRequestsApproval()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        _environment.DirectoryExists(LocalRoot).Returns(false);

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "newrepo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("local_repository_invalid");
        await _approvalPrompt.DidNotReceive().RequestApprovalAsync(
            Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        allowlist.TryGet("newrepo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenApprovalIsDenied_DoesNotMutateAllowlistOrWriteFile()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Denied());

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "newrepo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("approval_denied");
        allowlist.TryGet("newrepo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenApprovalTimesOut_ReturnsTimedOutAndDoesNotMutate()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Failure(ApprovalOutcome.TimedOut));

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "newrepo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("approval_timed_out");
        allowlist.TryGet("newrepo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenWriteFails_DoesNotMutateAllowlist()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new() { FailNextWrite = true };
        RepositoryRegistrationService service = CreateService(allowlist, store);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved());

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "newrepo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("registration_write_failed");
        allowlist.TryGet("newrepo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenApproved_WritesStoreAndUpdatesAllowlist()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        RepositoryRegistrationService service = CreateService(allowlist, store);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved());

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "newrepo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeTrue();
        outcome.Workspace.Should().Be("example-workspace");
        outcome.Slug.Should().Be("new-repo");
        allowlist.TryGet("newrepo", out RepositoryOptions? registered).Should().BeTrue();
        registered!.ProtectedBranches.Should().BeEquivalentTo(["main"]);
        registered.DirectPushBranches.Should().BeEquivalentTo(["develop"]);

        IReadOnlyDictionary<string, RepositoryOptions> stored = await store.LoadAllAsync(
            TestContext.Current.CancellationToken);
        stored.Should().ContainKey("newrepo");
    }

    [Fact]
    public async Task RegisterAsync_WhenTokenIsMissing_SavesTokenFromApproval()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        _tokenStore.Read("newrepo").Returns(ApiTokenStoreResult.Failure(ApiTokenStoreError.TokenNotFound));
        _tokenStore.Save("newrepo", "new-token").Returns(ApiTokenStoreResult.Success());
        _approvalPrompt.RequestApprovalAsync(
                Arg.Is<ApprovalPromptRequest>(request => request.TokenRequired),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved("new-token"));

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "newrepo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeTrue();
        _tokenStore.Received(1).Save("newrepo", "new-token");
    }

    [Fact]
    public async Task RegisterAsync_WhenAnotherRegistrationIsInProgress_ReturnsRegistrationInProgress()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        TaskCompletionSource<ApprovalPromptOutcome> pendingApproval = new();
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => pendingApproval.Task);

        Task<RepositoryRegistrationOutcome> firstCall = service.RegisterAsync(
            "newrepo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);
        while (!_approvalPrompt.ReceivedCalls().Any())
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        RepositoryRegistrationOutcome secondOutcome = await service.RegisterAsync(
            "anotherrepo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        secondOutcome.IsSuccess.Should().BeFalse();
        secondOutcome.Error!.Code.Should().Be("registration_in_progress");

        pendingApproval.SetResult(ApprovalPromptOutcome.Denied());
        await firstCall;
    }

    private RepositoryAllowlist CreateAllowlist() => new(new BuckettieOptions
    {
        AtlassianEmail = "developer@example.com",
        BitbucketUsername = "developer",
        Repositories = new Dictionary<string, RepositoryOptions>(),
    });

    private RepositoryRegistrationService CreateService(RepositoryAllowlist allowlist, FakeRepositoryStore? store = null)
    {
        RepositoryRegistrationValidator validator = new(allowlist, _environment, _git);
        return new RepositoryRegistrationService(
            validator,
            allowlist,
            store ?? new FakeRepositoryStore(),
            _tokenStore,
            _approvalPrompt,
            new RepositoryMutationGate());
    }
}
