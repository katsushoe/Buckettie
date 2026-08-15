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
        string json = "{\"repositories\":{\"buckettie\":" + ValidRepositoryJson + "}}";
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
        result.Options!.Repositories["buckettie"].RequireCleanWorkingTree.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WhenRepositoryIdIsDuplicated_ReturnsDuplicateError()
    {
        string json = "{\"repositories\":{\"buckettie\":" + ValidRepositoryJson
            + ",\"buckettie\":" + ValidRepositoryJson + "}}";
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.DuplicateRepositoryId);
    }

    [Fact]
    public async Task LoadAsync_WhenRepositoryIdIsInvalid_ReturnsRepositoryIdError()
    {
        string json = "{\"repositories\":{\"../repository\":" + ValidRepositoryJson + "}}";
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.InvalidRepositoryId);
    }

    [Fact]
    public async Task LoadAsync_WhenRequiredPropertyIsMissing_ReturnsRequiredError()
    {
        string repositoryJson = ValidRepositoryJson.Replace(
            "\"slug\": \"buckettie\",",
            string.Empty,
            StringComparison.Ordinal);
        string json = "{\"repositories\":{\"buckettie\":" + repositoryJson + "}}";
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
        string json = "{\"repositories\":{\"buckettie\":" + repositoryJson + "}}";
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.InvalidTagPattern);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"repositories\":{},\"unknown_property\":true}")]
    public async Task LoadAsync_WhenJsonContractIsInvalid_ReturnsInvalidJson(string json)
    {
        await using MemoryStream stream = CreateStream(json);

        ConfigurationLoadResult result = await _loader.LoadAsync(stream, TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ConfigurationErrorCode.InvalidJson);
    }

    private static MemoryStream CreateStream(string json) => new(Encoding.UTF8.GetBytes(json));
}
