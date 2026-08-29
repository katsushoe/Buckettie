using Buckettie.Application.Configuration;
using Buckettie.Infrastructure.Repositories;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class SqliteRepositoryStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"buckettie-repository-store-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task LoadAllAsync_WhenDatabaseIsNew_ReturnsEmpty()
    {
        SqliteRepositoryStore store = CreateStore();

        IReadOnlyDictionary<string, RepositoryOptions> repositories = await store.LoadAllAsync(
            TestContext.Current.CancellationToken);

        repositories.Should().BeEmpty();
    }

    [Fact]
    public async Task InsertAsync_WhenIdIsNew_SucceedsAndRoundTripsAllFields()
    {
        SqliteRepositoryStore store = CreateStore();
        RepositoryOptions repository = CreateRepository();

        bool inserted = await store.InsertAsync("buckettie", repository, TestContext.Current.CancellationToken);

        inserted.Should().BeTrue();
        IReadOnlyDictionary<string, RepositoryOptions> repositories = await store.LoadAllAsync(
            TestContext.Current.CancellationToken);
        repositories.Should().ContainKey("buckettie");
        repositories["buckettie"].Should().BeEquivalentTo(repository);
    }

    [Fact]
    public async Task InsertAsync_WhenIdAlreadyExists_ReturnsFalseAndDoesNotReplace()
    {
        SqliteRepositoryStore store = CreateStore();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);

        bool inserted = await store.InsertAsync(
            "buckettie", CreateRepository() with { Slug = "different" }, TestContext.Current.CancellationToken);

        inserted.Should().BeFalse();
        IReadOnlyDictionary<string, RepositoryOptions> repositories = await store.LoadAllAsync(
            TestContext.Current.CancellationToken);
        repositories["buckettie"].Slug.Should().Be("buckettie");
    }

    [Fact]
    public async Task InsertAsync_WhenIdDiffersOnlyByCase_ReturnsFalse()
    {
        SqliteRepositoryStore store = CreateStore();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);

        bool inserted = await store.InsertAsync(
            "BUCKETTIE", CreateRepository(), TestContext.Current.CancellationToken);

        inserted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenIdExists_ReplacesFields()
    {
        SqliteRepositoryStore store = CreateStore();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);
        RepositoryOptions updated = CreateRepository() with { TagTargetBranch = "release" };

        bool result = await store.UpdateAsync("buckettie", updated, TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        IReadOnlyDictionary<string, RepositoryOptions> repositories = await store.LoadAllAsync(
            TestContext.Current.CancellationToken);
        repositories["buckettie"].TagTargetBranch.Should().Be("release");
    }

    [Fact]
    public async Task UpdateAsync_WhenIdUsesDifferentCase_ReplacesFields()
    {
        SqliteRepositoryStore store = CreateStore();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);

        bool result = await store.UpdateAsync(
            "BUCKETTIE", CreateRepository() with { TagTargetBranch = "release" },
            TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WhenIdDoesNotExist_ReturnsFalse()
    {
        SqliteRepositoryStore store = CreateStore();

        bool result = await store.UpdateAsync("unknown", CreateRepository(), TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenIdExists_RemovesIt()
    {
        SqliteRepositoryStore store = CreateStore();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);

        bool result = await store.DeleteAsync("buckettie", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        IReadOnlyDictionary<string, RepositoryOptions> repositories = await store.LoadAllAsync(
            TestContext.Current.CancellationToken);
        repositories.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WhenIdUsesDifferentCase_RemovesIt()
    {
        SqliteRepositoryStore store = CreateStore();
        await store.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);

        bool result = await store.DeleteAsync("BUCKETTIE", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenIdDoesNotExist_ReturnsFalse()
    {
        SqliteRepositoryStore store = CreateStore();

        bool result = await store.DeleteAsync("unknown", TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAllAsync_WhenReopeningSameFile_PersistsAcrossInstances()
    {
        string databasePath = Path.Combine(_directory, "repositories.db");
        SqliteRepositoryStore first = new(databasePath);
        await first.InsertAsync("buckettie", CreateRepository(), TestContext.Current.CancellationToken);

        SqliteRepositoryStore second = new(databasePath);
        IReadOnlyDictionary<string, RepositoryOptions> repositories = await second.LoadAllAsync(
            TestContext.Current.CancellationToken);

        repositories.Should().ContainKey("buckettie");
    }

    private SqliteRepositoryStore CreateStore() =>
        new(Path.Combine(_directory, "repositories.db"));

    private static RepositoryOptions CreateRepository() => new()
    {
        Workspace = "example-workspace",
        Slug = "buckettie",
        LocalRoot = "repository-root",
        Remote = "origin",
        DevelopBranch = "develop",
        MainBranch = "main",
        DirectPushBranches = new HashSet<string> { "develop" },
        PullBranches = new HashSet<string> { "develop", "main" },
        ProtectedBranches = new HashSet<string> { "main" },
        TagTargetBranch = "main",
        TagPattern = "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
        RequireCleanWorkingTree = true,
    };
}
