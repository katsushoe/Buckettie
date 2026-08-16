using System.Text;
using Buckettie.Application.Credentials;
using Buckettie.Infrastructure.Credentials;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class DpapiFileTokenStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"buckettie-dpapi-{Guid.NewGuid():N}");
    private readonly FakeProtector _protector = new();
    private readonly FakeSecurity _security = new();

    [Fact]
    public void SaveAndRead_WhenTokenIsValid_UsesProtectedRepositoryFile()
    {
        DpapiFileTokenStore store = new(_directory, _protector, _security);
        store.Save("example", "secret-token").IsSuccess.Should().BeTrue();
        Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(_directory, "example.token"))).Should().NotContain("secret-token");
        store.Read("example").Token.Should().Be("secret-token");
        _security.WasApplied.Should().BeTrue();
    }

    [Fact]
    public void Read_WhenFileDoesNotExist_ReturnsTokenNotFound()
    {
        DpapiFileTokenStore store = new(_directory, _protector, _security);
        store.Read("example").Error.Should().Be(ApiTokenStoreError.TokenNotFound);
    }

    [Fact]
    public void Delete_WhenFileDoesNotExist_IsIdempotent()
    {
        DpapiFileTokenStore store = new(_directory, _protector, _security);
        store.Delete("example").IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("../example")]
    [InlineData("example/name")]
    public void Save_WhenRepositoryIsInvalid_DoesNotWrite(string repository)
    {
        DpapiFileTokenStore store = new(_directory, _protector, _security);
        store.Save(repository, "secret").Error.Should().Be(ApiTokenStoreError.InvalidRepositoryId);
        Directory.Exists(_directory).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class FakeProtector : IDpapiProtector
    {
        public byte[] Protect(byte[] clearText, byte[] entropy) => clearText.Select(value => (byte)(value ^ 0x5A)).ToArray();
        public byte[] Unprotect(byte[] protectedData, byte[] entropy) => Protect(protectedData, entropy);
    }

    private sealed class FakeSecurity : ISecretDirectorySecurity
    {
        public bool WasApplied { get; private set; }
        public void Ensure(string directory) { Directory.CreateDirectory(directory); WasApplied = true; }
    }
}
