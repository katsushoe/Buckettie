using Buckettie.Application.Credentials;
using Buckettie.Application.Git;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class GitAskPassResponderTests
{
    private readonly IApiTokenStore _tokenStore = Substitute.For<IApiTokenStore>();

    [Fact]
    public void Respond_WhenUsernameIsRequested_ReturnsConfiguredEmail()
    {
        GitAskPassResponder responder = new(_tokenStore);

        GitAskPassResponse result = responder.Respond(
            "buckettie",
            "developer@example.com",
            "Username for 'https://bitbucket.org':");

        result.Value.Should().Be("developer@example.com");
        _tokenStore.DidNotReceiveWithAnyArgs().Read(default!);
    }

    [Fact]
    public void Respond_WhenPasswordIsRequested_ReturnsCredentialManagerToken()
    {
        _tokenStore.Read("buckettie").Returns(ApiTokenStoreResult.Success("secret-token"));
        GitAskPassResponder responder = new(_tokenStore);

        GitAskPassResponse result = responder.Respond(
            "buckettie",
            "developer@example.com",
            "Password for 'https://developer@bitbucket.org':");

        result.Value.Should().Be("secret-token");
    }

    [Fact]
    public void Respond_WhenTokenIsMissing_ReturnsTokenUnavailable()
    {
        _tokenStore.Read("buckettie").Returns(ApiTokenStoreResult.Failure(ApiTokenStoreError.TokenNotFound));
        GitAskPassResponder responder = new(_tokenStore);

        GitAskPassResponse result = responder.Respond(
            "buckettie",
            "developer@example.com",
            "Password for 'https://developer@bitbucket.org':");

        result.Error.Should().Be(GitAskPassError.TokenUnavailable);
    }

    [Fact]
    public void Respond_WhenPromptIsUnsupported_ReturnsUnsupportedPrompt()
    {
        GitAskPassResponder responder = new(_tokenStore);

        GitAskPassResponse result = responder.Respond("buckettie", "developer@example.com", "PIN:");

        result.Error.Should().Be(GitAskPassError.UnsupportedPrompt);
    }

    [Fact]
    public void CreateEnvironment_WhenCalled_ContainsNoToken()
    {
        IReadOnlyDictionary<string, string> environment = GitAskPassProtocol.CreateEnvironment(
            Path.GetFullPath("askpass.exe"),
            "buckettie",
            "developer@example.com");

        environment.Should().ContainKey("GIT_ASKPASS").WhoseValue.Should().Be(Path.GetFullPath("askpass.exe"));
        environment.Should().NotContainValue("secret-token");
    }

    [Theory]
    [InlineData("relative-askpass.exe", "developer@example.com")]
    [InlineData("C:\\AskPass.exe", "developer@example.com\r\nsecret")]
    public void CreateEnvironment_WhenInputIsUnsafe_Throws(string executable, string username)
    {
        Action action = () => GitAskPassProtocol.CreateEnvironment(executable, "buckettie", username);

        action.Should().Throw<ArgumentException>();
    }
}
