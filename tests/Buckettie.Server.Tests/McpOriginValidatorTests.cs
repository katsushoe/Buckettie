using FluentAssertions;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class McpOriginValidatorTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("http://localhost:45450", true)]
    [InlineData("http://127.0.0.1:45450", true)]
    [InlineData("http://localhost:45451", false)]
    [InlineData("https://localhost:45450", false)]
    [InlineData("https://attacker.example", false)]
    [InlineData("not-a-uri", false)]
    public void IsAllowed_WhenOriginIsChecked_ReturnsExpectedResult(string? origin, bool expected)
    {
        McpOriginValidator.IsAllowed(origin, 45450).Should().Be(expected);
    }
}
