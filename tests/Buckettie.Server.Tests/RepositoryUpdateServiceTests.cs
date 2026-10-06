using Buckettie.Application.Configuration;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class RepositoryUpdateServiceTests
{
    private readonly IInteractiveApprovalPrompt _approvalPrompt = Substitute.For<IInteractiveApprovalPrompt>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_WithMoyaiBinding_ChangesOnlyAfterApproval(bool approved)
    {
        Guid project = Guid.NewGuid();
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(approved ? ApprovalPromptOutcome.Approved() : ApprovalPromptOutcome.Denied());
        RepositoryUpdateService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUpdateOutcome outcome = await service.UpdateAsync("buckettie",
            CreateRequest() with { MoyaiProjectId = project }, TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().Be(approved);
        RepositoryOptions stored = (await store.LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"];
        stored.MoyaiProjectId.Should().Be(approved ? project : null);
        await _approvalPrompt.Received(1).RequestApprovalAsync(
            Arg.Is<ApprovalPromptRequest>(request => request.MoyaiProjectId == project.ToString("D")),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_RemovingMoyaiBinding_ChangesOnlyAfterApproval(bool approved)
    {
        Guid project = Guid.NewGuid();
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved());
        RepositoryUpdateService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());
        (await service.UpdateAsync("buckettie", CreateRequest() with { MoyaiProjectId = project },
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(approved ? ApprovalPromptOutcome.Approved() : ApprovalPromptOutcome.Denied());

        RepositoryUpdateOutcome outcome = await service.UpdateAsync("buckettie",
            CreateRequest() with { RemoveMoyaiProjectId = true }, TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().Be(approved);
        RepositoryOptions stored = (await store.LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"];
        stored.MoyaiProjectId.Should().Be(approved ? null : project);
    }

    [Fact]
    public async Task UpdateAsync_WhenApproved_WritesStoreAndUpdatesAllowlistBranchPolicyOnly()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved());
        RepositoryUpdateService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUpdateOutcome outcome = await service.UpdateAsync(
            "buckettie", CreateRequest() with { TagTargetBranch = "release" },
            TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeTrue();
        allowlist.TryGet("buckettie", out RepositoryOptions? updated).Should().BeTrue();
        updated!.TagTargetBranch.Should().Be("release");
        updated.Workspace.Should().Be("example-workspace");
        updated.LocalRoot.Should().Be("repository-root");
        (await store.LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"].TagTargetBranch
            .Should().Be("release");
    }

    [Fact]
    public async Task UpdateAsync_WhenRepositoryIdUsesDifferentCase_UpdatesRepository()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved());
        RepositoryUpdateService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUpdateOutcome outcome = await service.UpdateAsync(
            "BUCKETTIE", CreateRequest() with { TagTargetBranch = "release" },
            TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WhenApprovalIsDenied_DoesNotMutate()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Denied());
        RepositoryUpdateService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        RepositoryUpdateOutcome outcome = await service.UpdateAsync(
            "buckettie", CreateRequest() with { TagTargetBranch = "release" },
            TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("approval_denied");
        allowlist.TryGet("buckettie", out RepositoryOptions? unchanged).Should().BeTrue();
        unchanged!.TagTargetBranch.Should().Be("main");
    }

    [Fact]
    public async Task UpdateAsync_WhenRepositoryIsNotRegistered_NeverRequestsApproval()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryUpdateService service = new(
            allowlist, new FakeRepositoryStore(), _approvalPrompt, new RepositoryMutationGate());

        RepositoryUpdateOutcome outcome = await service.UpdateAsync(
            "unknown", CreateRequest(), TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("repository_not_registered");
        await _approvalPrompt.DidNotReceive().RequestApprovalAsync(
            Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenTagPatternIsInvalid_NeverRequestsApproval()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryUpdateService service = new(
            allowlist, new FakeRepositoryStore(), _approvalPrompt, new RepositoryMutationGate());

        RepositoryUpdateOutcome outcome = await service.UpdateAsync(
            "buckettie", CreateRequest() with { TagPattern = "(" }, TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("tag_pattern_invalid");
        await _approvalPrompt.DidNotReceive().RequestApprovalAsync(
            Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithCommitAuthor_StoresPairAndKeepsItWhenOmittedLater()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        FakeRepositoryStore store = new();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved());
        RepositoryUpdateService service = new(allowlist, store, _approvalPrompt, new RepositoryMutationGate());

        (await service.UpdateAsync("buckettie", CreateRequest() with
            { CommitAuthorName = "Registered Author", CommitAuthorEmail = "registered@example.com" },
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
        (await service.UpdateAsync("buckettie", CreateRequest() with { TagTargetBranch = "release" },
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        RepositoryOptions stored = (await store.LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"];
        stored.CommitAuthorName.Should().Be("Registered Author");
        stored.CommitAuthorEmail.Should().Be("registered@example.com");
        stored.TagTargetBranch.Should().Be("release");
    }

    [Theory]
    [InlineData("Only Name", null)]
    [InlineData(null, "only@example.com")]
    [InlineData("Name", "not-an-email")]
    [InlineData("Bad\nName", "a@example.com")]
    public async Task UpdateAsync_WithIncompleteOrInvalidAuthor_NeverRequestsApproval(string? name, string? email)
    {
        RepositoryUpdateService service = new(
            CreateAllowlist(), new FakeRepositoryStore(), _approvalPrompt, new RepositoryMutationGate());

        RepositoryUpdateOutcome outcome = await service.UpdateAsync("buckettie",
            CreateRequest() with { CommitAuthorName = name, CommitAuthorEmail = email },
            TestContext.Current.CancellationToken);

        outcome.Error!.Code.Should().Be("commit_author_invalid");
        await _approvalPrompt.DidNotReceive().RequestApprovalAsync(
            Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private static RepositoryAllowlist CreateAllowlist() => new(new BuckettieOptions
    {
        AtlassianEmail = "developer@example.com",
        BitbucketUsername = "developer",
        Repositories = new Dictionary<string, RepositoryOptions> { ["buckettie"] = CreateRepository() },
    });

    private static RepositoryUpdateRequest CreateRequest() => new(
        new HashSet<string> { "develop" },
        new HashSet<string> { "develop", "main" },
        new HashSet<string> { "main" },
        "main",
        "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
        true);

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
