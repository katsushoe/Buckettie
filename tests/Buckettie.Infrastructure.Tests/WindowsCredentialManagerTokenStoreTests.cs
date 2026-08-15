using System.Text;
using Buckettie.Application.Credentials;
using Buckettie.Infrastructure.Credentials;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class WindowsCredentialManagerTokenStoreTests
{
    private readonly FakeCredentialApi _api = new();

    [Fact]
    public void Save_WhenTokenIsValid_WritesRepositoryScopedCredential()
    {
        WindowsCredentialManagerTokenStore store = new(_api);

        ApiTokenStoreResult result = store.Save("buckettie", "secret-token");

        result.IsSuccess.Should().BeTrue();
        _api.TargetName.Should().Be("Buckettie/Bitbucket/buckettie");
        _api.UserName.Should().Be("buckettie");
        _api.WrittenSecret.Should().Equal(Encoding.UTF8.GetBytes("secret-token"));
    }

    [Fact]
    public void Save_WhenTokenIsEmpty_ReturnsInvalidToken()
    {
        WindowsCredentialManagerTokenStore store = new(_api);

        ApiTokenStoreResult result = store.Save("buckettie", string.Empty);

        result.Error.Should().Be(ApiTokenStoreError.InvalidToken);
        _api.WriteCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../repository")]
    [InlineData("repository/name")]
    public void Read_WhenRepositoryIdIsInvalid_ReturnsInvalidRepositoryId(string repositoryId)
    {
        WindowsCredentialManagerTokenStore store = new(_api);

        ApiTokenStoreResult result = store.Read(repositoryId);

        result.Error.Should().Be(ApiTokenStoreError.InvalidRepositoryId);
    }

    [Fact]
    public void Read_WhenTokenExists_ReturnsTokenAndClearsProviderBuffer()
    {
        byte[] providerSecret = Encoding.UTF8.GetBytes("secret-token");
        _api.ReadResult = CredentialApiResult.Success(providerSecret);
        WindowsCredentialManagerTokenStore store = new(_api);

        ApiTokenStoreResult result = store.Read("buckettie");

        result.Token.Should().Be("secret-token");
        providerSecret.Should().OnlyContain(value => value == 0);
    }

    [Fact]
    public void Read_WhenTokenWasSavedByWindowsUi_ReturnsUtf16Token()
    {
        byte[] providerSecret = Encoding.Unicode.GetBytes("secret-token");
        _api.ReadResult = CredentialApiResult.Success(providerSecret);
        WindowsCredentialManagerTokenStore store = new(_api);

        ApiTokenStoreResult result = store.Read("buckettie");

        result.Token.Should().Be("secret-token");
        providerSecret.Should().OnlyContain(value => value == 0);
    }

    [Fact]
    public void Read_WhenTokenDoesNotExist_ReturnsNotFound()
    {
        _api.ReadResult = CredentialApiResult.Failure(1168);
        WindowsCredentialManagerTokenStore store = new(_api);

        ApiTokenStoreResult result = store.Read("buckettie");

        result.Error.Should().Be(ApiTokenStoreError.TokenNotFound);
    }

    [Fact]
    public void Read_WhenPlatformIsNotSupported_ReturnsPlatformError()
    {
        _api.ReadResult = CredentialApiResult.Failure(-1);
        WindowsCredentialManagerTokenStore store = new(_api);

        ApiTokenStoreResult result = store.Read("buckettie");

        result.Error.Should().Be(ApiTokenStoreError.PlatformNotSupported);
    }

    [Fact]
    public void Delete_WhenTokenDoesNotExist_ReturnsSuccess()
    {
        _api.DeleteResult = CredentialApiResult.Failure(1168);
        WindowsCredentialManagerTokenStore store = new(_api);

        ApiTokenStoreResult result = store.Delete("buckettie");

        result.IsSuccess.Should().BeTrue();
    }

    private sealed class FakeCredentialApi : IWindowsCredentialApi
    {
        internal string? TargetName { get; private set; }

        internal string? UserName { get; private set; }

        internal byte[]? WrittenSecret { get; private set; }

        internal int WriteCount { get; private set; }

        internal CredentialApiResult ReadResult { get; set; } = CredentialApiResult.Failure(1168);

        internal CredentialApiResult DeleteResult { get; set; } = CredentialApiResult.Success();

        public CredentialApiResult Write(string targetName, string userName, byte[] secret)
        {
            WriteCount++;
            TargetName = targetName;
            UserName = userName;
            WrittenSecret = secret.ToArray();
            return CredentialApiResult.Success();
        }

        public CredentialApiResult Read(string targetName)
        {
            TargetName = targetName;
            return ReadResult;
        }

        public CredentialApiResult Delete(string targetName)
        {
            TargetName = targetName;
            return DeleteResult;
        }
    }
}
