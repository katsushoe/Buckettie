using System.Text;
using Buckettie.Application.Configuration;
using Buckettie.Infrastructure.Configuration;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class JsonBuckettieOptionsLoaderTests
{
    private const string ValidRepositoryJson = """
        {
          "workspace": "example-workspace",
          "slug": "buckettie",
          "local_root": "repository-root",
          "remote": "origin",
          "develop_branch": "develop",
          "main_branch": "main",
          "direct_push_branches": ["develop"],
          "pull_branches": ["develop", "main"],
          "protected_branches": ["main"],
          "tag_target_branch": "main",
          "tag_pattern": "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
          "require_clean_working_tree": true
        }
        """;

    private readonly JsonBuckettieOptionsLoader _loader = new();

    [Fact]
    public async Task LoadAsync_WhenSnakeCaseJsonIsValid_ReturnsOptions()
    {
        string json = CreateRootJson("\"buckettie\":" + ValidRepositoryJson);
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
        result.Options!.Repositories["buckettie"].RequireCleanWorkingTree.Should().BeTrue();
        result.Options.McpPort.Should().Be(45450);
        result.Options.McpPath.Should().Be("/mcp");
    }

    [Theory]
    [InlineData("\"mcp_port\":0,")]
    [InlineData("\"mcp_port\":65536,")]
    public async Task LoadAsync_WhenMcpPortIsInvalid_ReturnsPortError(string property)
    {
        string json = CreateRootJson("\"buckettie\":" + ValidRepositoryJson, property);
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ConfigurationErrorCode.InvalidMcpPort);
    }

    [Fact]
    public async Task LoadAsync_WhenMcpPathIsInvalid_ReturnsPathError()
    {
        string json = CreateRootJson("\"buckettie\":" + ValidRepositoryJson, "\"mcp_path\":\"external\",");
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ConfigurationErrorCode.InvalidMcpPath);
    }

    [Fact]
    public async Task LoadAsync_WhenRepositoryIdIsDuplicated_ReturnsDuplicateError()
    {
        string json = CreateRootJson("\"buckettie\":" + ValidRepositoryJson
            + ",\"buckettie\":" + ValidRepositoryJson);
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.DuplicateRepositoryId);
    }

    [Fact]
    public async Task LoadAsync_WhenRepositoryIdIsInvalid_ReturnsRepositoryIdError()
    {
        string json = CreateRootJson("\"../repository\":" + ValidRepositoryJson);
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.InvalidRepositoryId);
    }

    [Fact]
    public async Task LoadAsync_WhenAtlassianEmailIsInvalid_ReturnsEmailError()
    {
        string json = CreateRootJson("\"buckettie\":" + ValidRepositoryJson)
            .Replace("developer@example.com", "not-an-email", StringComparison.Ordinal);
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.InvalidAtlassianEmail);
    }

    [Fact]
    public async Task LoadAsync_WhenBitbucketUsernameIsInvalid_ReturnsUsernameError()
    {
        string json = CreateRootJson("\"buckettie\":" + ValidRepositoryJson)
            .Replace("developer\",", "invalid username\",", StringComparison.Ordinal);
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.InvalidBitbucketUsername);
    }

    [Fact]
    public async Task LoadAsync_WhenRequiredPropertyIsMissing_ReturnsRequiredError()
    {
        string repositoryJson = ValidRepositoryJson.Replace(
            "\"slug\": \"buckettie\",",
            string.Empty,
            StringComparison.Ordinal);
        string json = CreateRootJson("\"buckettie\":" + repositoryJson);
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Should().Be(new ConfigurationError(
                ConfigurationErrorCode.RequiredValueMissing,
                "repositories.buckettie.slug"));
    }

    [Fact]
    public async Task LoadAsync_WhenTagPatternIsInvalid_ReturnsPatternError()
    {
        string repositoryJson = ValidRepositoryJson.Replace(
            "^v[0-9]+\\\\.[0-9]+\\\\.[0-9]+.*$",
            "[",
            StringComparison.Ordinal);
        string json = CreateRootJson("\"buckettie\":" + repositoryJson);
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.InvalidTagPattern);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"atlassian_email\":\"developer@example.com\",\"bitbucket_username\":\"developer\",\"repositories\":{},\"unknown_property\":true}")]
    public async Task LoadAsync_WhenJsonContractIsInvalid_ReturnsInvalidJson(string json)
    {
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.InvalidJson);
    }

    private static MemoryStream CreateStream(string json) => new(Encoding.UTF8.GetBytes(json));

    private static string CreateRootJson(string repositories, string additionalProperty = "") =>
        "{" + additionalProperty
        + "\"atlassian_email\":\"developer@example.com\",\"bitbucket_username\":\"developer\",\"repositories\":{"
        + repositories + "}}";
}
