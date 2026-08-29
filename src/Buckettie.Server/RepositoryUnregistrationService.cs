using Buckettie.Application.Repositories;

namespace Buckettie.Server;

/// <summary>Repository登録解除要求を1つの流れとして実行する境界です。</summary>
public interface IRepositoryUnregistrationService
{
    /// <summary>Repositoryの登録解除を試みます。</summary>
    Task<RepositoryUnregistrationOutcome> UnregisterAsync(string repositoryId, CancellationToken cancellationToken);
}

/// <summary>
/// Repository登録解除を実行します。Push/PR/Tag権限を削減するだけの操作のため、
/// 登録・修正と異なり対話Desktopでの人間承認は要求しません。
/// </summary>
internal sealed class RepositoryUnregistrationService : IRepositoryUnregistrationService
{
    private readonly RepositoryAllowlist _allowlist;
    private readonly IRepositoryStore _repositoryStore;
    private readonly RepositoryMutationGate _gate;

    /// <summary>登録解除Serviceを初期化します。</summary>
    public RepositoryUnregistrationService(
        RepositoryAllowlist allowlist,
        IRepositoryStore repositoryStore,
        RepositoryMutationGate gate)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(repositoryStore);
        ArgumentNullException.ThrowIfNull(gate);
        _allowlist = allowlist;
        _repositoryStore = repositoryStore;
        _gate = gate;
    }

    /// <inheritdoc />
    public async Task<RepositoryUnregistrationOutcome> UnregisterAsync(
        string repositoryId, CancellationToken cancellationToken)
    {
        if (!RepositoryId.IsLookupValid(repositoryId))
        {
            return RepositoryUnregistrationOutcome.Failure(
                BuckettieToolResultMapper.RegistrationValidationError(RepositoryValidationError.RepositoryIdInvalid));
        }

        if (!await _gate.TryEnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return RepositoryUnregistrationOutcome.Failure(BuckettieToolResultMapper.RegistrationInProgressError());
        }

        try
        {
            if (!_allowlist.TryGet(repositoryId, out _))
            {
                return RepositoryUnregistrationOutcome.Failure(
                    BuckettieToolResultMapper.RegistrationValidationError(
                        RepositoryValidationError.RepositoryNotRegistered));
            }

            bool deleted = await _repositoryStore.DeleteAsync(repositoryId, cancellationToken)
                .ConfigureAwait(false);
            if (!deleted)
            {
                return RepositoryUnregistrationOutcome.Failure(BuckettieToolResultMapper.RegistrationWriteFailedError());
            }

            _allowlist.Unregister(repositoryId);
            return RepositoryUnregistrationOutcome.Success(repositoryId);
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>Repository登録解除要求の結果です。</summary>
public sealed record RepositoryUnregistrationOutcome(bool IsSuccess, string? RepositoryId, BuckettieToolError? Error)
{
    /// <summary>成功結果を生成します。</summary>
    public static RepositoryUnregistrationOutcome Success(string repositoryId) => new(true, repositoryId, null);

    /// <summary>失敗結果を生成します。</summary>
    public static RepositoryUnregistrationOutcome Failure(BuckettieToolError error) => new(false, null, error);
}
