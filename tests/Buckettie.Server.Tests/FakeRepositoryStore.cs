using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;

namespace Buckettie.Server.Tests;

/// <summary>In-memoryのIRepositoryStore Fakeです。書き込み失敗を明示的にSimulateできます。</summary>
internal sealed class FakeRepositoryStore : IRepositoryStore
{
    private readonly Dictionary<string, RepositoryOptions> _repositories = new(StringComparer.Ordinal);

    /// <summary>次回のInsert/Update/Delete呼び出しを1回だけ失敗させます。</summary>
    public bool FailNextWrite { get; set; }

    public Task<IReadOnlyDictionary<string, RepositoryOptions>> LoadAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, RepositoryOptions>>(
            new Dictionary<string, RepositoryOptions>(_repositories, StringComparer.Ordinal));

    public Task<bool> InsertAsync(string repositoryId, RepositoryOptions options, CancellationToken cancellationToken)
    {
        if (ConsumeFailure())
        {
            return Task.FromResult(false);
        }

        if (_repositories.ContainsKey(repositoryId))
        {
            return Task.FromResult(false);
        }

        _repositories[repositoryId] = options;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateAsync(string repositoryId, RepositoryOptions options, CancellationToken cancellationToken)
    {
        if (ConsumeFailure())
        {
            return Task.FromResult(false);
        }

        if (!_repositories.ContainsKey(repositoryId))
        {
            return Task.FromResult(false);
        }

        _repositories[repositoryId] = options;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string repositoryId, CancellationToken cancellationToken)
    {
        if (ConsumeFailure())
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_repositories.Remove(repositoryId));
    }

    private bool ConsumeFailure()
    {
        if (!FailNextWrite)
        {
            return false;
        }

        FailNextWrite = false;
        return true;
    }
}
