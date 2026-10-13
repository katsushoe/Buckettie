using Buckettie.Server;
using FluentAssertions;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class ProviderBranchSwitchTests
{
    [Fact]
    public void Scope_RequiresRepositoryBranchWrite()
    {
        ProviderToolPolicy.Scopes("buckettie_branch_switch").Should().Equal("repository.branch.write");
    }
}
