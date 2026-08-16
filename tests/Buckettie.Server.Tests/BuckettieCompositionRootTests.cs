using System.Text;
using Buckettie.Application.Configuration;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Credentials;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Configuration;
using Buckettie.Infrastructure.Credentials;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class BuckettieCompositionRootTests
{
    [Fact]
    public async Task CreateAsync_WhenConfigurationIsValid_ResolvesRuntimeServices()
    {
        await using MemoryStream configuration = CreateConfiguration();
        string askPassExecutable = Path.GetFullPath("Buckettie.AskPass.exe");

        using BuckettieCompositionResult result = await BuckettieCompositionRoot.CreateAsync(
            configuration,
            askPassExecutable,
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Services.Should().NotBeNull();
        result.Services!.GetRequiredService<IBuckettieOptionsLoader>()
            .Should().BeOfType<JsonBuckettieOptionsLoader>();
        result.Services.GetRequiredService<IApiTokenStore>()
            .Should().BeOfType<DpapiFileTokenStore>();
        result.Services.GetRequiredService<IGitGateway>()
            .Should().NotBeNull();
        result.Services.GetRequiredService<IBitbucketRepositoryGateway>()
            .Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenConfigurationIsInvalid_ReturnsErrorsWithoutServices()
    {
        await using MemoryStream configuration = new(Encoding.UTF8.GetBytes("{}"));

        using BuckettieCompositionResult result = await BuckettieCompositionRoot.CreateAsync(
            configuration,
            Path.GetFullPath("Buckettie.AskPass.exe"),
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Services.Should().BeNull();
        result.Errors.Should().ContainSingle();
    }

    private static MemoryStream CreateConfiguration()
    {
        const string json = """
            {
              "atlassian_email": "developer@example.com",
              "bitbucket_username": "developer",
              "repositories": {
                "example": {
                  "workspace": "example-workspace",
                  "slug": "example-repository",
                  "local_root": "D:\\Projects\\ExampleRepository",
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
              }
            }
            """;
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }
}
