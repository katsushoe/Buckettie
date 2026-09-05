using System.Diagnostics;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;
using Microsoft.Extensions.Logging;

namespace Buckettie.Server;

internal sealed record BuckettieAuditEvent(
    string Tool,
    string Repository,
    string? Branch,
    int? PullRequestId,
    string? Tag,
    bool IsSuccess,
    long DurationMilliseconds,
    string? ErrorCode,
    string? CorrelationId = null,
    string? Source = null,
    string? SourceKind = null,
    string? SourceHash = null,
    string? Actor = null,
    string? Reason = null,
    string? Target = null,
    string? OldHead = null,
    string? NewHead = null,
    string? RecoveryReference = null);

internal interface IBuckettieAuditLogger
{
    void Write(BuckettieAuditEvent auditEvent);
}

internal sealed class BuckettieAuditLogger(ILogger<BuckettieAuditLogger> logger) : IBuckettieAuditLogger
{
    public void Write(BuckettieAuditEvent auditEvent) => logger.LogInformation(
        "client={Client} tool={Tool} repository={Repository} branch={Branch} pull_request_id={PullRequestId} tag={Tag} result={Result} duration_ms={DurationMilliseconds} error_code={ErrorCode} correlation_id={CorrelationId} source={Source} source_kind={SourceKind} source_hash={SourceHash} actor={Actor} reason={Reason} target={Target} old_head={OldHead} new_head={NewHead} recovery_ref={RecoveryReference}",
        "mcp",
        auditEvent.Tool,
        auditEvent.Repository,
        auditEvent.Branch ?? "-",
        auditEvent.PullRequestId?.ToString() ?? "-",
        auditEvent.Tag ?? "-",
        auditEvent.IsSuccess ? "success" : "failure",
        auditEvent.DurationMilliseconds,
        auditEvent.ErrorCode ?? "-",
        auditEvent.CorrelationId ?? "-",
        auditEvent.Source ?? "-",
        auditEvent.SourceKind ?? "-",
        auditEvent.SourceHash ?? "-",
        auditEvent.Actor ?? "-",
        auditEvent.Reason ?? "-",
        auditEvent.Target ?? "-",
        auditEvent.OldHead ?? "-",
        auditEvent.NewHead ?? "-",
        auditEvent.RecoveryReference ?? "-");
}

internal sealed class AuditedGitGateway(IGitGateway inner, IBuckettieAuditLogger audit) : IGitGateway
{
    public Task<GitGatewayResult> GetStatusAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_repository_status", repository, () => inner.GetStatusAsync(repository, cancellationToken));

    public Task<GitGatewayResult> GetDiffAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_repository_diff", repository, () => inner.GetDiffAsync(repository, cancellationToken));

    public Task<GitGatewayResult> CommitAsync(
        string repository, string message, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_repository_commit", repository, () => inner.CommitAsync(repository, message, cancellationToken));

    public Task<GitGatewayResult> FetchAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_fetch", repository, () => inner.FetchAsync(repository, cancellationToken));

    public Task<GitGatewayResult> PullAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_pull", repository, () => inner.PullAsync(repository, cancellationToken));

    public Task<GitGatewayResult> PushAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_push", repository, () => inner.PushAsync(repository, cancellationToken));

    public Task<GitGatewayResult> PushTagAsync(
        string repository, string tag, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_tag_push", repository, () => inner.PushTagAsync(repository, tag, cancellationToken));

    public Task<GitGatewayResult> PreviewHistoryRewriteAsync(
        string repository, GitHistoryRewriteRequest request, CancellationToken cancellationToken = default) =>
        RunHistoryAsync("bitbucket_history_rewrite_preview", repository, request.Branch,
            request.ExpectedOldHead, request.Reason,
            () => inner.PreviewHistoryRewriteAsync(repository, request, cancellationToken));

    public Task<GitGatewayResult> RewriteHistoryAsync(
        string repository, GitHistoryRewriteRequest request, CancellationToken cancellationToken = default) =>
        RunHistoryAsync("bitbucket_history_rewrite_execute", repository, request.Branch,
            request.ExpectedOldHead, request.Reason,
            () => inner.RewriteHistoryAsync(repository, request, cancellationToken));

    public Task<GitGatewayResult> ForcePushWithLeaseAsync(
        string repository, GitForceWithLeaseRequest request, CancellationToken cancellationToken = default) =>
        RunHistoryAsync("bitbucket_force_push_with_lease", repository, request.Branch,
            request.ExpectedRemoteHead, request.Reason,
            () => inner.ForcePushWithLeaseAsync(repository, request, cancellationToken));

    private async Task<GitGatewayResult> RunHistoryAsync(
        string tool, string repository, string branch, string oldHead, string reason,
        Func<Task<GitGatewayResult>> operation)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        GitGatewayResult result = await operation().ConfigureAwait(false);
        audit.Write(new(tool, repository, branch, null, null, result.IsSuccess,
            stopwatch.ElapsedMilliseconds, result.Error?.ToString(), result.CorrelationId,
            Actor: "mcp", Reason: reason, Target: $"refs/heads/{branch}", OldHead: oldHead,
            NewHead: result.HistoryRewrite?.NewHead ?? result.ForceWithLease?.NewLocalHead,
            RecoveryReference: result.HistoryRewrite?.RecoveryReference));
        return result;
    }

    private async Task<GitGatewayResult> RunAsync(string tool, string repository, Func<Task<GitGatewayResult>> operation)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        GitGatewayResult result = await operation().ConfigureAwait(false);
        audit.Write(new(tool, repository, result.Branch, null, null, result.IsSuccess,
            stopwatch.ElapsedMilliseconds, result.Error?.ToString(), result.CorrelationId));
        return result;
    }
}

internal sealed class AuditedBitbucketRepositoryGateway(
    IBitbucketRepositoryGateway inner,
    IBuckettieAuditLogger audit) : IBitbucketRepositoryGateway
{
    public Task<BitbucketResult<BitbucketRepositoryInfo>> GetRepositoryAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_repository_get", repository, null, null, null, () => inner.GetRepositoryAsync(repository, cancellationToken));

    public Task<BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>> ListBranchesAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_branch_list", repository, null, null, null, () => inner.ListBranchesAsync(repository, cancellationToken));

    public Task<BitbucketResult<BitbucketBranchInfo>> GetBranchAsync(string repository, string branch, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_branch_get", repository, branch, null, null, () => inner.GetBranchAsync(repository, branch, cancellationToken));

    public Task<BitbucketResult<BitbucketBranchInfo>> CreateBranchAsync(string repository, string branch, string source, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_branch_create", repository, branch, null, null,
            () => inner.CreateBranchAsync(repository, branch, source, cancellationToken),
            BranchSource.IsValid(source) ? source : null);

    public Task<BitbucketResult<bool>> DeleteBranchAsync(string repository, string branch, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_branch_delete", repository, branch, null, null, () => inner.DeleteBranchAsync(repository, branch, cancellationToken));

    public Task<BitbucketResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_tag_list", repository, null, null, null, () => inner.ListTagsAsync(repository, cancellationToken));

    public Task<BitbucketResult<BitbucketTagInfo>> GetTagAsync(string repository, string tag, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_tag_get", repository, null, null, tag, () => inner.GetTagAsync(repository, tag, cancellationToken));

    public Task<BitbucketResult<BitbucketTagInfo>> CreateTagAsync(string repository, BitbucketTagCreate input, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_tag_create", repository, null, null, input.Name, () => inner.CreateTagAsync(repository, input, cancellationToken));

    public Task<BitbucketResult<bool>> DeleteTagAsync(string repository, string tag, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_tag_delete", repository, null, null, tag, () => inner.DeleteTagAsync(repository, tag, cancellationToken));

    public Task<BitbucketResult<BitbucketReleaseInfo>> CreateReleaseAsync(string repository, string version, string? notes, CancellationToken cancellationToken = default) =>
        RunAsync("buckettie_release_create", repository, null, null, version, () => inner.CreateReleaseAsync(repository, version, notes, cancellationToken));

    public Task<BitbucketResult<BitbucketReleaseInfo>> PublishReleaseAsync(string repository, string version, string? artifactPath, string? notes, CancellationToken cancellationToken = default) =>
        RunAsync("buckettie_release_publish", repository, null, null, version, () => inner.PublishReleaseAsync(repository, version, artifactPath, notes, cancellationToken));

    public Task<BitbucketResult<BitbucketReleaseInfo>> GetReleaseAsync(string repository, string version, CancellationToken cancellationToken = default) =>
        RunAsync("buckettie_release_get", repository, null, null, version, () => inner.GetReleaseAsync(repository, version, cancellationToken));

    public Task<BitbucketResult<bool>> WithdrawReleaseAsync(string repository, string version, CancellationToken cancellationToken = default) =>
        RunAsync("buckettie_release_withdraw", repository, null, null, version, () => inner.WithdrawReleaseAsync(repository, version, cancellationToken));

    public Task<BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(string repository, BitbucketPullRequestState? state, string? source, string? destination, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_pr_list", repository, source, null, null, () => inner.ListPullRequestsAsync(repository, state, source, destination, cancellationToken));

    public Task<BitbucketResult<BitbucketPullRequestInfo>> GetPullRequestAsync(string repository, int pullRequestId, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_pr_get", repository, null, pullRequestId, null, () => inner.GetPullRequestAsync(repository, pullRequestId, cancellationToken));

    public Task<BitbucketResult<string>> GetPullRequestDiffAsync(string repository, int pullRequestId, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_pr_diff", repository, null, pullRequestId, null, () => inner.GetPullRequestDiffAsync(repository, pullRequestId, cancellationToken));

    public Task<BitbucketResult<BitbucketPullRequestInfo>> CreatePullRequestAsync(string repository, BitbucketPullRequestCreate input, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_pr_create", repository, null, null, null, () => inner.CreatePullRequestAsync(repository, input, cancellationToken));

    public Task<BitbucketResult<BitbucketPullRequestInfo>> MergePullRequestAsync(string repository, int pullRequestId, BitbucketPullRequestMerge input, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_pr_merge", repository, null, pullRequestId, null, () => inner.MergePullRequestAsync(repository, pullRequestId, input, cancellationToken));

    private async Task<BitbucketResult<T>> RunAsync<T>(string tool, string repository, string? branch,
        int? pullRequestId, string? tag, Func<Task<BitbucketResult<T>>> operation, string? source = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        BitbucketResult<T> result = await operation().ConfigureAwait(false);
        int? auditedPullRequestId = pullRequestId;
        if (auditedPullRequestId is null && result.Value is BitbucketPullRequestInfo pullRequest)
        {
            auditedPullRequestId = pullRequest.Id;
        }
        BitbucketBranchInfo? createdBranch = result.Value as BitbucketBranchInfo;
        audit.Write(new(tool, repository, branch, auditedPullRequestId, tag, result.IsSuccess,
            stopwatch.ElapsedMilliseconds, result.Error?.ToString(), Source: source,
            SourceKind: createdBranch?.SourceKind, SourceHash: createdBranch?.SourceHash));
        return result;
    }
}

internal sealed class AuditedRepositoryRegistrationService(
    IRepositoryRegistrationService inner,
    IBuckettieAuditLogger audit) : IRepositoryRegistrationService
{
    public async Task<RepositoryRegistrationOutcome> RegisterAsync(
        string repositoryId,
        string localRoot,
        string remote,
        string developBranch,
        string mainBranch,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        RepositoryRegistrationOutcome result = await inner.RegisterAsync(
            repositoryId, localRoot, remote, developBranch, mainBranch, cancellationToken).ConfigureAwait(false);
        audit.Write(new(
            "bitbucket_repository_register",
            repositoryId,
            null,
            null,
            null,
            result.IsSuccess,
            stopwatch.ElapsedMilliseconds,
            result.Error?.Code));
        return result;
    }
}

internal sealed class AuditedRepositoryUnregistrationService(
    IRepositoryUnregistrationService inner,
    IBuckettieAuditLogger audit) : IRepositoryUnregistrationService
{
    public async Task<RepositoryUnregistrationOutcome> UnregisterAsync(
        string repositoryId, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        RepositoryUnregistrationOutcome result = await inner.UnregisterAsync(repositoryId, cancellationToken)
            .ConfigureAwait(false);
        audit.Write(new(
            "bitbucket_repository_unregister",
            repositoryId,
            null,
            null,
            null,
            result.IsSuccess,
            stopwatch.ElapsedMilliseconds,
            result.Error?.Code));
        return result;
    }
}

internal sealed class AuditedRepositoryUpdateService(
    IRepositoryUpdateService inner,
    IBuckettieAuditLogger audit) : IRepositoryUpdateService
{
    public async Task<RepositoryUpdateOutcome> UpdateAsync(
        string repositoryId, RepositoryUpdateRequest request, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        RepositoryUpdateOutcome result = await inner.UpdateAsync(repositoryId, request, cancellationToken)
            .ConfigureAwait(false);
        audit.Write(new(
            "bitbucket_repository_update",
            repositoryId,
            null,
            null,
            null,
            result.IsSuccess,
            stopwatch.ElapsedMilliseconds,
            result.Error?.Code));
        return result;
    }
}
