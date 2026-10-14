using FluentAssertions;
using Buckettie.Application.Configuration;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class AuditedRepositoryUpdateServiceTests
{
    private readonly IRepositoryUpdateService _inner = Substitute.For<IRepositoryUpdateService>();
    private readonly IBuckettieAuditLogger _audit = Substitute.For<IBuckettieAuditLogger>();

    private static readonly RepositoryUpdateRequest Request = new(
        new HashSet<string> { "develop" },
        new HashSet<string> { "develop", "main" },
        new HashSet<string> { "main" },
        "main",
        "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
        true);

    [Fact]
    public async Task UpdateAsync_WhenSuccessful_WritesSuccessAuditEvent()
    {
        RepositoryUpdateOutcome outcome = RepositoryUpdateOutcome.Success("buckettie");
        _inner.UpdateAsync("buckettie", Request, Arg.Any<CancellationToken>()).Returns(outcome);
        AuditedRepositoryUpdateService service = new(_inner, _audit);

        await service.UpdateAsync("buckettie", Request, TestContext.Current.CancellationToken);

        _audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(auditEvent =>
            auditEvent.Tool == "bitbucket_repository_update"
            && auditEvent.Repository == "buckettie"
            && auditEvent.IsSuccess
            && auditEvent.ErrorCode == null));
    }

    [Fact]
    public async Task UpdateAsync_WhenDenied_WritesFailureAuditEventWithErrorCode()
    {
        RepositoryUpdateOutcome outcome = RepositoryUpdateOutcome.Failure(
            new BuckettieToolError("approval_denied", "The repository update was denied."));
        _inner.UpdateAsync("buckettie", Request, Arg.Any<CancellationToken>()).Returns(outcome);
        AuditedRepositoryUpdateService service = new(_inner, _audit);

        await service.UpdateAsync("buckettie", Request, TestContext.Current.CancellationToken);

        _audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(auditEvent =>
            !auditEvent.IsSuccess && auditEvent.ErrorCode == "approval_denied"));
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenSuccessful_WritesSettingsToolAuditEvent()
    {
        RepositoryOptions options = new()
        {
            Workspace = "workspace", Slug = "buckettie", LocalRoot = "root", DevelopBranch = "develop",
            MainBranch = "main", DirectPushBranches = ["develop"], PullBranches = ["develop"],
            ProtectedBranches = ["main"], TagTargetBranch = "main", TagPattern = "^v.*$",
        };
        RepositoryUpdateOutcome outcome = RepositoryUpdateOutcome.Success("buckettie", options, options);
        RepositorySettingsChanges changes = new(new HashSet<string> { "require_clean_working_tree" },
            RequireCleanWorkingTree: false);
        _inner.UpdateSettingsAsync("buckettie", changes, Arg.Any<CancellationToken>(),
            Arg.Any<Func<CancellationToken, Task>?>(), Arg.Any<bool>()).Returns(outcome);
        AuditedRepositoryUpdateService service = new(_inner, _audit);

        await service.UpdateSettingsAsync("buckettie", changes, TestContext.Current.CancellationToken);

        _audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(auditEvent =>
            auditEvent.Tool == "bitbucket_repository_settings_update"
            && auditEvent.Repository == "buckettie"
            && auditEvent.IsSuccess));
    }
}
