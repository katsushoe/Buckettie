using Buckettie.Application.Git;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class AuditedGitGatewayTests
{
    [Fact]
    public async Task SwitchBranchAsync_RecordsAuditedToolAndRequestedBranch()
    {
        IGitGateway inner = Substitute.For<IGitGateway>();
        IBuckettieAuditLogger audit = Substitute.For<IBuckettieAuditLogger>();
        inner.SwitchBranchAsync("example", "feature/work", Arg.Any<CancellationToken>())
            .Returns(GitGatewayResult.Success("branch_switch", "example", "feature/work"));
        AuditedGitGateway gateway = new(inner, audit);

        await gateway.SwitchBranchAsync("example", "feature/work", TestContext.Current.CancellationToken);

        audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(entry =>
            entry.Tool == "buckettie_branch_switch"
            && entry.Repository == "example"
            && entry.Branch == "feature/work"
            && entry.IsSuccess));
    }
}
