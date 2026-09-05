using System.Text.Json;
using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using Microsoft.Data.Sqlite;

namespace Buckettie.Infrastructure.Repositories;

/// <summary>
/// Repository設定をSQLite Databaseへ永続化するIRepositoryStore実装です。
/// Serviceが稼働したままRegister/Unregister/Updateを反映できるよう、
/// buckettie.json全体の書き換えではなく1行単位のInsert/Update/Deleteで完結します。
/// </summary>
public sealed class SqliteRepositoryStore : IRepositoryStore
{
    private readonly string _connectionString;

    /// <summary>指定したDatabase Fileを使ってStoreを初期化します。</summary>
    public SqliteRepositoryStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        string fullPath = Path.GetFullPath(databasePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        using SqliteConnection connection = OpenConnection();
        using SqliteCommand create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS repositories (
                repository_id TEXT PRIMARY KEY COLLATE NOCASE,
                workspace TEXT NOT NULL,
                slug TEXT NOT NULL,
                local_root TEXT NOT NULL,
                remote TEXT NOT NULL,
                develop_branch TEXT NOT NULL,
                main_branch TEXT NOT NULL,
                direct_push_branches TEXT NOT NULL,
                pull_branches TEXT NOT NULL,
                protected_branches TEXT NOT NULL,
                tag_target_branch TEXT NOT NULL,
                tag_pattern TEXT NOT NULL,
                require_clean_working_tree INTEGER NOT NULL,
                history_rewrite_branches TEXT NOT NULL DEFAULT '[]'
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_repositories_repository_id_nocase
                ON repositories(repository_id COLLATE NOCASE);
            """;
        create.ExecuteNonQuery();
        EnsureHistoryRewriteColumn(connection);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, RepositoryOptions>> LoadAllAsync(
        CancellationToken cancellationToken)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand select = connection.CreateCommand();
        select.CommandText = """
            SELECT repository_id, workspace, slug, local_root, remote, develop_branch, main_branch,
                   direct_push_branches, pull_branches, protected_branches, tag_target_branch,
                   tag_pattern, require_clean_working_tree, history_rewrite_branches
            FROM repositories;
            """;

        Dictionary<string, RepositoryOptions> repositories = new(StringComparer.OrdinalIgnoreCase);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string repositoryId = reader.GetString(0);
            repositories[repositoryId] = new RepositoryOptions
            {
                Workspace = reader.GetString(1),
                Slug = reader.GetString(2),
                LocalRoot = reader.GetString(3),
                Remote = reader.GetString(4),
                DevelopBranch = reader.GetString(5),
                MainBranch = reader.GetString(6),
                DirectPushBranches = DeserializeSet(reader.GetString(7)),
                PullBranches = DeserializeSet(reader.GetString(8)),
                ProtectedBranches = DeserializeSet(reader.GetString(9)),
                TagTargetBranch = reader.GetString(10),
                TagPattern = reader.GetString(11),
                RequireCleanWorkingTree = reader.GetInt64(12) != 0,
                HistoryRewriteBranches = DeserializeSet(reader.GetString(13)),
            };
        }

        return repositories;
    }

    /// <inheritdoc />
    public async Task<bool> InsertAsync(
        string repositoryId, RepositoryOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);

        using SqliteConnection connection = OpenConnection();
        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT OR IGNORE INTO repositories
                (repository_id, workspace, slug, local_root, remote, develop_branch, main_branch,
                 direct_push_branches, pull_branches, protected_branches, tag_target_branch,
                 tag_pattern, require_clean_working_tree, history_rewrite_branches)
            VALUES
                (@id, @workspace, @slug, @localRoot, @remote, @developBranch, @mainBranch,
                 @directPushBranches, @pullBranches, @protectedBranches, @tagTargetBranch,
                 @tagPattern, @requireCleanWorkingTree, @historyRewriteBranches);
            """;
        BindOptions(insert, repositoryId, options);
        int affected = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        string repositoryId, RepositoryOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);

        using SqliteConnection connection = OpenConnection();
        using SqliteCommand update = connection.CreateCommand();
        update.CommandText = """
            UPDATE repositories SET
                workspace = @workspace,
                slug = @slug,
                local_root = @localRoot,
                remote = @remote,
                develop_branch = @developBranch,
                main_branch = @mainBranch,
                direct_push_branches = @directPushBranches,
                pull_branches = @pullBranches,
                protected_branches = @protectedBranches,
                tag_target_branch = @tagTargetBranch,
                tag_pattern = @tagPattern,
                require_clean_working_tree = @requireCleanWorkingTree,
                history_rewrite_branches = @historyRewriteBranches
            WHERE repository_id = @id COLLATE NOCASE;
            """;
        BindOptions(update, repositoryId, options);
        int affected = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string repositoryId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        using SqliteConnection connection = OpenConnection();
        using SqliteCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM repositories WHERE repository_id = @id COLLATE NOCASE;";
        delete.Parameters.AddWithValue("@id", repositoryId);
        int affected = await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(_connectionString);
        connection.Open();
        return connection;
    }

    private static void BindOptions(SqliteCommand command, string repositoryId, RepositoryOptions options)
    {
        command.Parameters.AddWithValue("@id", repositoryId);
        command.Parameters.AddWithValue("@workspace", options.Workspace);
        command.Parameters.AddWithValue("@slug", options.Slug);
        command.Parameters.AddWithValue("@localRoot", options.LocalRoot);
        command.Parameters.AddWithValue("@remote", options.Remote);
        command.Parameters.AddWithValue("@developBranch", options.DevelopBranch);
        command.Parameters.AddWithValue("@mainBranch", options.MainBranch);
        command.Parameters.AddWithValue("@directPushBranches", SerializeSet(options.DirectPushBranches));
        command.Parameters.AddWithValue("@pullBranches", SerializeSet(options.PullBranches));
        command.Parameters.AddWithValue("@protectedBranches", SerializeSet(options.ProtectedBranches));
        command.Parameters.AddWithValue("@tagTargetBranch", options.TagTargetBranch);
        command.Parameters.AddWithValue("@tagPattern", options.TagPattern);
        command.Parameters.AddWithValue("@requireCleanWorkingTree", options.RequireCleanWorkingTree ? 1 : 0);
        command.Parameters.AddWithValue("@historyRewriteBranches", SerializeSet(options.HistoryRewriteBranches));
    }

    private static void EnsureHistoryRewriteColumn(SqliteConnection connection)
    {
        using SqliteCommand columns = connection.CreateCommand();
        columns.CommandText = "PRAGMA table_info(repositories);";
        using SqliteDataReader reader = columns.ExecuteReader();
        bool exists = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), "history_rewrite_branches", StringComparison.Ordinal))
            {
                exists = true;
                break;
            }
        }
        reader.Close();
        if (exists) return;
        using SqliteCommand alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE repositories ADD COLUMN history_rewrite_branches TEXT NOT NULL DEFAULT '[]';";
        alter.ExecuteNonQuery();
    }

    private static string SerializeSet(HashSet<string> value) => JsonSerializer.Serialize(value);

    private static HashSet<string> DeserializeSet(string json) =>
        new(JsonSerializer.Deserialize<string[]>(json) ?? [], StringComparer.Ordinal);
}
