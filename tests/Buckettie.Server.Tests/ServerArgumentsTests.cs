using FluentAssertions;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class ServerArgumentsTests
{
    [Fact]
    public void Parse_WithoutArguments_DefaultsToStandalone()
    {
        ServerArguments parsed = ServerArguments.Parse([]);

        parsed.ConfigurationPath.Should().BeNull();
        parsed.MoyaiIntegration.Should().BeFalse();
    }

    [Theory]
    [InlineData(new[] { "C:\\Buckettie\\config\\buckettie.json" }, false)]
    [InlineData(new[] { "C:\\Buckettie\\config\\buckettie.json", "--moyai" }, true)]
    [InlineData(new[] { "--moyai", "C:\\Buckettie\\config\\buckettie.json" }, true)]
    [InlineData(new[] { "--MOYAI", "C:\\Buckettie\\config\\buckettie.json" }, true)]
    public void Parse_ConfigurationAndMoyaiOption_InAnyOrder(string[] args, bool moyai)
    {
        ServerArguments parsed = ServerArguments.Parse(args);

        parsed.ConfigurationPath.Should().Be("C:\\Buckettie\\config\\buckettie.json");
        parsed.MoyaiIntegration.Should().Be(moyai);
    }

    [Theory]
    [InlineData("--standalone")]
    [InlineData("first.json|second.json")]
    public void Parse_UnknownOptionOrSecondPath_Rejects(string serialized)
    {
        Action parse = () => ServerArguments.Parse(serialized.Split('|'));

        parse.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(false, "standalone")]
    [InlineData(true, "moyai")]
    public void IntegrationMode_Name_MatchesCapabilityValue(bool moyai, string expected) =>
        new ProviderIntegrationMode(moyai).Name.Should().Be(expected);

    [Fact]
    public void Parse_DirectUnrestrictedWithMoyai_EnablesUnrestrictedDirectConnection()
    {
        ServerArguments parsed = ServerArguments.Parse(["--moyai", "--direct-unrestricted"]);

        parsed.MoyaiIntegration.Should().BeTrue();
        parsed.DirectUnrestricted.Should().BeTrue();
    }

    [Fact]
    public void Parse_DirectUnrestrictedWithoutMoyai_Rejects()
    {
        Action parse = () => ServerArguments.Parse(["--direct-unrestricted"]);

        parse.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(false, false, "unrestricted")]
    [InlineData(true, false, "read_only")]
    [InlineData(true, true, "unrestricted")]
    public void IntegrationMode_DirectConnection_MatchesContractValue(bool moyai, bool direct, string expected) =>
        new ProviderIntegrationMode(moyai, direct).DirectConnection.Should().Be(expected);
}
