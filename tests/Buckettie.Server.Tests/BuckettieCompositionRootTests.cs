using System.Text;
using Buckettie.Application.Configuration;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Credentials;
using Buckettie.Application.Git;
using Buckettie.Application.Repositories;
using Buckettie.Infrastructure.Configuration;
using Buckettie.Infrastructure.Credentials;
using Buckettie.Infrastructure.Repositories;
using Buckettie.Server;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class BuckettieCompositionRootTests : IDisposable
{
    private readonly string _temporaryDirectory =
        Path.Combine(Path.GetTempPath(), $"buckettie-composition-{Guid.NewGuid():N}");
    private readonly string _askPassExecutable;
    private readonly string _approvalPromptExecutable;
    private readonly string _configurationPath;
    private readonly string _databasePath;

    public BuckettieCompositionRootTests()
    {
        string binaryDirectory = Path.Combine(_temporaryDirectory, "bin");
        Directory.CreateDirectory(binaryDirectory);
        _askPassExecutable = Path.Combine(binaryDirectory, "Buckettie.AskPass.exe");
        _approvalPromptExecutable = Path.Combine(binaryDirectory, "Buckettie.ApprovalPrompt.exe");
        _configurationPath = Path.Combine(_temporaryDirectory, "buckettie.json");
        _databasePath = Path.Combine(_temporaryDirectory, "data", "repositories.db");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task CreateAsync_WhenConfigurationIsValid_ResolvesRuntimeServices()
    {
        await using MemoryStream configuration = CreateConfiguration();

        using BuckettieCompositionResult result = await CreateAsync(configuration);

        result.IsSuccess.Should().BeTrue();
        result.Services.Should().NotBeNull();
        result.Services!.GetRequiredService<IBuckettieOptionsLoader>()
            .Should().BeOfType<JsonBuckettieOptionsLoader>();
        result.Services.GetRequiredService<IRepositoryStore>()
            .Should().BeOfType<SqliteRepositoryStore>();
        result.Services.GetRequiredService<IApiTokenStore>()
            .Should().BeOfType<DpapiFileTokenStore>();
        result.Services.GetRequiredService<IGitGateway>()
            .Should().NotBeNull();
        result.Services.GetRequiredService<IBitbucketRepositoryGateway>()
            .Should().NotBeNull();
        result.Services.GetRequiredService<IRepositoryRegistrationService>()
            .Should().NotBeNull();
        result.Services.GetRequiredService<IRepositoryUnregistrationService>()
            .Should().NotBeNull();
        result.Services.GetRequiredService<IRepositoryUpdateService>()
            .Should().NotBeNull();
        result.Services.GetRequiredService<BuckettieOptions>().Repositories.Should().ContainKey("example");
    }

    [Fact]
    public async Task CreateAsync_WhenConfigurationIsInvalid_ReturnsErrorsWithoutServices()
    {
        await using MemoryStream configuration = new(Encoding.UTF8.GetBytes("{}"));

        using BuckettieCompositionResult result = await CreateAsync(configuration);

        result.IsSuccess.Should().BeFalse();
        result.Services.Should().BeNull();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_WhenJsonHasLegacyRepositories_MigratesIntoStoreAndRewritesJsonToEmpty()
    {
        await using MemoryStream configuration = CreateConfiguration();

        using BuckettieCompositionResult result = await CreateAsync(configuration);
        result.IsSuccess.Should().BeTrue();

        SqliteRepositoryStore store = new(_databasePath);
        IReadOnlyDictionary<string, RepositoryOptions> stored = await store.LoadAllAsync(
            TestContext.Current.CancellationToken);
        stored.Should().ContainKey("example");

        await using FileStream reloadedStream = File.OpenRead(_configurationPath);
        JsonBuckettieOptionsLoader loader = new();
        ConfigurationLoadResult reloaded = await loader.LoadAsync(
            reloadedStream, TestContext.Current.CancellationToken);
        reloaded.IsValid.Should().BeTrue();
        reloaded.Options!.Repositories.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenStoreAlreadyHasRepositories_IgnoresJsonRepositoriesAndDoesNotRewriteJson()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        SqliteRepositoryStore preseeded = new(_databasePath);
        await preseeded.InsertAsync(
            "already-migrated", CreateRepository(), TestContext.Current.CancellationToken);

        await using MemoryStream configuration = CreateConfiguration();

        using BuckettieCompositionResult result = await CreateAsync(configuration);

        result.IsSuccess.Should().BeTrue();
        BuckettieOptions options = result.Services!.GetRequiredService<BuckettieOptions>();
        options.Repositories.Should().ContainKey("already-migrated");
        options.Repositories.Should().NotContainKey("example");
        File.Exists(_configurationPath).Should().BeFalse();
    }

    private Task<BuckettieCompositionResult> CreateAsync(Stream configuration) => BuckettieCompositionRoot.CreateAsync(
        configuration,
        _configurationPath,
        _askPassExecutable,
        _approvalPromptExecutable,
        TimeSpan.FromSeconds(30),
        TestContext.Current.CancellationToken);

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

    private static RepositoryOptions CreateRepository() => new()
    {
        Workspace = "example-workspace",
        Slug = "already-migrated",
        LocalRoot = "D:\\Projects\\AlreadyMigrated",
        Remote = "origin",
        DevelopBranch = "develop",
        MainBranch = "main",
        DirectPushBranches = new HashSet<string> { "develop" },
        PullBranches = new HashSet<string> { "develop", "main" },
        ProtectedBranches = new HashSet<string> { "main" },
        TagTargetBranch = "main",
        TagPattern = "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
    };
}
