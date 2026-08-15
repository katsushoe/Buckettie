using Buckettie.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class GitEnvironmentSanitizerTests
{
    [Fact]
    public void Sanitize_WhenGitOverridesAreInherited_RemovesOverridesOnly()
    {
        Dictionary<string, string?> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_ASKPASS"] = "untrusted.exe",
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "http.extraHeader",
            ["GIT_CONFIG_VALUE_0"] = "Authorization: secret",
            ["GIT_SSH_COMMAND"] = "untrusted-command",
            ["PATH"] = "trusted-path",
        };

        GitEnvironmentSanitizer.Sanitize(environment);

        environment.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string?>("PATH", "trusted-path"));
    }
}
