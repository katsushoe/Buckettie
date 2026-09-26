using Buckettie.Application.Configuration;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class RepositoryUnregistrationServiceTests
{
    private readonly IInteractiveApprovalPrompt _approvalPrompt = Substitute.For<IInteractiveApprovalPrompt>();

    public RepositoryUnregistrationServiceTests() =>
        _approvalPrompt.RequestApprovalAsync(Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new ApprovalPromptOutcome(ApprovalOutcome.Approved));

    [Fact]
    public async Task UnregisterAsync_WhenRepositoryIsRegistered_RemovesItFromStoreAndAllowlist()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        RepositoryUnregistrationService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUnregistrationOutcome outcome = await service.UnregisterAsync(
            "buckettie", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeTrue();
        allowlist.TryGet("buckettie", out _).Should().BeFalse();
        (await store.LoadAllAsync(TestContext.Current.CancellationToken)).Should().NotContainKey("buckettie");
    }

    [Fact]
    public async Task UnregisterAsync_WhenRepositoryIdUsesDifferentCase_RemovesRepository()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        RepositoryUnregistrationService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUnregistrationOutcome outcome = await service.UnregisterAsync(
            "BUCKETTIE", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterAsync_WhenRepositoryIsNotRegistered_ReturnsErrorAndDoesNotMutate()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        RepositoryUnregistrationService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUnregistrationOutcome outcome = await service.UnregisterAsync(
            "unknown", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("repository_not_registered");
    }

    [Fact]
    public async Task UnregisterAsync_WhenRepositoryIdIsInvalid_ReturnsValidationError()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryUnregistrationService service = new(allowlist, new FakeRepositoryStore(), _approvalPrompt, new RepositoryMutationGate());

        RepositoryUnregistrationOutcome outcome = await service.UnregisterAsync(
            "invalid id", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("repository_id_invalid");
    }

    [Fact]
    public async Task UnregisterAsync_WhenStoreWriteFails_DoesNotMutateAllowlist()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        store.FailNextWrite = true;
        RepositoryUnregistrationService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUnregistrationOutcome outcome = await service.UnregisterAsync(
            "buckettie", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("registration_write_failed");
        allowlist.TryGet("buckettie", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData(ApprovalOutcome.Denied)]
    [InlineData(ApprovalOutcome.TimedOut)]
    [InlineData(ApprovalOutcome.NoInteractiveSession)]
    public async Task UnregisterAsync_WhenNotApproved_DoesNotMutate(ApprovalOutcome outcomeValue)
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        _approvalPrompt.RequestApprovalAsync(Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new ApprovalPromptOutcome(outcomeValue));
        RepositoryUnregistrationService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUnregistrationOutcome outcome = await service.UnregisterAsync(
            "buckettie", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        allowlist.TryGet("buckettie", out _).Should().BeTrue();
        (await store.LoadAllAsync(TestContext.Current.CancellationToken)).Should().ContainKey("buckettie");
    }

    [Fact]
    public async Task UnregisterAsync_RequestsApprovalForUnregistrationOfRegisteredRepository()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        RepositoryUnregistrationService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        await service.UnregisterAsync("buckettie", TestContext.Current.CancellationToken);

        await _approvalPrompt.Received(1).RequestApprovalAsync(
            Arg.Is<ApprovalPromptRequest>(request => request.Operation == ApprovalOperation.Unregister
                && request.RepositoryId == "buckettie" && request.Workspace == "example-workspace"),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnregisterAsync_WhenRepositoryIsNotRegistered_DoesNotAskForApproval()
    {
        RepositoryUnregistrationService service = new(CreateAllowlist(), new FakeRepositoryStore(), _approvalPrompt,
            new RepositoryMutationGate());

        await service.UnregisterAsync("unknown", TestContext.Current.CancellationToken);

        _approvalPrompt.ReceivedCalls().Should().BeEmpty();
    }

    private static RepositoryAllowlist CreateAllowlist() => new(new BuckettieOptions
    {
        AtlassianEmail = "developer@example.com",
        BitbucketUsername = "developer",
        Repositories = new Dictionary<string, RepositoryOptions> { ["buckettie"] = CreateRepository() },
    });

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
