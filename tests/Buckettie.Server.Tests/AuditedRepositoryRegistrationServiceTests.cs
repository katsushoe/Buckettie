using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class AuditedRepositoryRegistrationServiceTests
{
    private readonly IRepositoryRegistrationService _inner = Substitute.For<IRepositoryRegistrationService>();
    private readonly IBuckettieAuditLogger _audit = Substitute.For<IBuckettieAuditLogger>();

    [Fact]
    public async Task RegisterAsync_WhenSuccessful_WritesSuccessAuditEventWithoutBranchOrTagFields()
    {
        RepositoryRegistrationOutcome outcome = RepositoryRegistrationOutcome.Success(
            "new-repo", "example-workspace", "new-repo");
        _inner.RegisterAsync(
                "new-repo", "C:\\Repositories\\NewRepo", "origin", "develop", "main", Arg.Any<CancellationToken>())
            .Returns(outcome);
        AuditedRepositoryRegistrationService service = new(_inner, _audit);

        await service.RegisterAsync(
            "new-repo", "C:\\Repositories\\NewRepo", "origin", "develop", "main",
            TestContext.Current.CancellationToken);

        _audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(auditEvent =>
            auditEvent.Tool == "bitbucket_repository_register"
            && auditEvent.Repository == "new-repo"
            && auditEvent.Branch == null
            && auditEvent.PullRequestId == null
            && auditEvent.Tag == null
            && auditEvent.IsSuccess
            && auditEvent.ErrorCode == null));
    }

    [Fact]
    public async Task RegisterAsync_WhenDenied_WritesFailureAuditEventWithErrorCode()
    {
        RepositoryRegistrationOutcome outcome = RepositoryRegistrationOutcome.Failure(
            new BuckettieToolError("approval_denied", "The repository registration was denied."));
        _inner.RegisterAsync(
                "new-repo", "C:\\Repositories\\NewRepo", "origin", "develop", "main", Arg.Any<CancellationToken>())
            .Returns(outcome);
        AuditedRepositoryRegistrationService service = new(_inner, _audit);

        await service.RegisterAsync(
            "new-repo", "C:\\Repositories\\NewRepo", "origin", "develop", "main",
            TestContext.Current.CancellationToken);

        _audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(auditEvent =>
            !auditEvent.IsSuccess && auditEvent.ErrorCode == "approval_denied"));
    }
}
