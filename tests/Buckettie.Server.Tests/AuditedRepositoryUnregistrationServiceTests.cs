using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class AuditedRepositoryUnregistrationServiceTests
{
    private readonly IRepositoryUnregistrationService _inner = Substitute.For<IRepositoryUnregistrationService>();
    private readonly IBuckettieAuditLogger _audit = Substitute.For<IBuckettieAuditLogger>();

    [Fact]
    public async Task UnregisterAsync_WhenSuccessful_WritesSuccessAuditEvent()
    {
        RepositoryUnregistrationOutcome outcome = RepositoryUnregistrationOutcome.Success("buckettie");
        _inner.UnregisterAsync("buckettie", Arg.Any<CancellationToken>()).Returns(outcome);
        AuditedRepositoryUnregistrationService service = new(_inner, _audit);

        await service.UnregisterAsync("buckettie", TestContext.Current.CancellationToken);

        _audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(auditEvent =>
            auditEvent.Tool == "bitbucket_repository_unregister"
            && auditEvent.Repository == "buckettie"
            && auditEvent.IsSuccess
            && auditEvent.ErrorCode == null));
    }

    [Fact]
    public async Task UnregisterAsync_WhenNotRegistered_WritesFailureAuditEventWithErrorCode()
    {
        RepositoryUnregistrationOutcome outcome = RepositoryUnregistrationOutcome.Failure(
            new BuckettieToolError("repository_not_registered", "The repository is not registered."));
        _inner.UnregisterAsync("unknown", Arg.Any<CancellationToken>()).Returns(outcome);
        AuditedRepositoryUnregistrationService service = new(_inner, _audit);

        await service.UnregisterAsync("unknown", TestContext.Current.CancellationToken);

        _audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(auditEvent =>
            !auditEvent.IsSuccess && auditEvent.ErrorCode == "repository_not_registered"));
    }
}
