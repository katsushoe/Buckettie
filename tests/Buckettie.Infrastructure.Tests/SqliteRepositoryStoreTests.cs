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

    [Fact]
    public async Task InsertAndUpdate_CommitAuthor_RoundTripsAndClearsToNull()
    {
        SqliteRepositoryStore store = CreateStore();
        RepositoryOptions withAuthor = CreateRepository() with
        { CommitAuthorName = "Registered Author", CommitAuthorEmail = "registered@example.com" };

        await store.InsertAsync("buckettie", withAuthor, TestContext.Current.CancellationToken);
        (await store.LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"]
            .Should().BeEquivalentTo(withAuthor);
        await store.UpdateAsync("buckettie", withAuthor with { CommitAuthorName = null, CommitAuthorEmail = null },
            TestContext.Current.CancellationToken);

        RepositoryOptions cleared = (await store.LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"];
        cleared.CommitAuthorName.Should().BeNull();
        cleared.CommitAuthorEmail.Should().BeNull();
    }

    [Fact]
    public async Task MoyaiBinding_InsertUpdateAndRemoval_SurviveDatabaseReopen()
    {
        SqliteRepositoryStore store = CreateStore();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        await store.InsertAsync("buckettie", CreateRepository() with { MoyaiProjectId = first },
            TestContext.Current.CancellationToken);
        (await CreateStore().LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"]
            .MoyaiProjectId.Should().Be(first);
        await store.UpdateAsync("buckettie", CreateRepository() with { MoyaiProjectId = second },
            TestContext.Current.CancellationToken);
        (await CreateStore().LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"]
            .MoyaiProjectId.Should().Be(second);
        await store.UpdateAsync("buckettie", CreateRepository() with { MoyaiProjectId = null },
            TestContext.Current.CancellationToken);
        (await CreateStore().LoadAllAsync(TestContext.Current.CancellationToken))["buckettie"]
            .MoyaiProjectId.Should().BeNull();
    }

    [Fact]
    public async Task Open_DatabaseWithoutAuthorColumns_AddsThemAndKeepsExistingRows()
    {
        Directory.CreateDirectory(_directory);
        string databasePath = Path.Combine(_directory, "legacy.db");
        using (Microsoft.Data.Sqlite.SqliteConnection connection = new($"Data Source={databasePath}"))
        {
            connection.Open();
            using Microsoft.Data.Sqlite.SqliteCommand create = connection.CreateCommand();
            // Schema before commit authors were stored (1.3.33.0 and earlier).
            create.CommandText = """
                CREATE TABLE repositories (
                    repository_id TEXT PRIMARY KEY COLLATE NOCASE, workspace TEXT NOT NULL, slug TEXT NOT NULL,
                    local_root TEXT NOT NULL, remote TEXT NOT NULL, develop_branch TEXT NOT NULL,
                    main_branch TEXT NOT NULL, direct_push_branches TEXT NOT NULL, pull_branches TEXT NOT NULL,
                    protected_branches TEXT NOT NULL, tag_target_branch TEXT NOT NULL, tag_pattern TEXT NOT NULL,
                    require_clean_working_tree INTEGER NOT NULL, history_rewrite_branches TEXT NOT NULL DEFAULT '[]');
                INSERT INTO repositories VALUES ('legacy', 'ws', 'slug', 'root', 'origin', 'develop', 'main',
                    '["develop"]', '["develop"]', '["main"]', 'main', '^v.*$', 1, '[]');
                """;
            create.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        SqliteRepositoryStore store = new(databasePath);
        RepositoryOptions legacy = (await store.LoadAllAsync(TestContext.Current.CancellationToken))["legacy"];

        legacy.Slug.Should().Be("slug");
        legacy.CommitAuthorName.Should().BeNull();
        legacy.CommitAuthorEmail.Should().BeNull();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
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
