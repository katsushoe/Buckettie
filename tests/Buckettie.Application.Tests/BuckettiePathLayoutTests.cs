using Buckettie.Application.Configuration;
using FluentAssertions;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class BuckettiePathLayoutTests
{
    [Fact]
    public void FromBinaryDirectory_WhenStandardBinIsSpecified_ReturnsStandardLayout()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuckettiePathLayoutTests", "install");

        BuckettiePathLayout result = BuckettiePathLayout.FromBinaryDirectory(
            Path.Combine(root, "bin") + Path.DirectorySeparatorChar);

        result.InstallRoot.Should().Be(Path.GetFullPath(root));
        result.BinaryDirectory.Should().Be(Path.Combine(root, "bin"));
        result.ConfigurationDirectory.Should().Be(Path.Combine(root, "config"));
        result.LogDirectory.Should().Be(Path.Combine(root, "logs"));
        result.DataDirectory.Should().Be(Path.Combine(root, "data"));
        result.SecretDirectory.Should().Be(Path.Combine(root, "data", "secrets"));
    }

    [Fact]
    public void FromBinaryDirectory_WhenValueIsEmpty_ThrowsArgumentException()
    {
        Action action = () => BuckettiePathLayout.FromBinaryDirectory(string.Empty);

        action.Should().Throw<ArgumentException>();
    }
}
