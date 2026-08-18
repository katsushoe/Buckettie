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
    string? ErrorCode);

internal interface IBuckettieAuditLogger
{
    void Write(BuckettieAuditEvent auditEvent);
}

internal sealed class BuckettieAuditLogger(ILogger<BuckettieAuditLogger> logger) : IBuckettieAuditLogger
{
    public void Write(BuckettieAuditEvent auditEvent) => logger.LogInformation(
        "client={Client} tool={Tool} repository={Repository} branch={Branch} pull_request_id={PullRequestId} tag={Tag} result={Result} duration_ms={DurationMilliseconds} error_code={ErrorCode}",
        "mcp",
        auditEvent.Tool,
        auditEvent.Repository,
        auditEvent.Branch ?? "-",
        auditEvent.PullRequestId?.ToString() ?? "-",
        auditEvent.Tag ?? "-",
        auditEvent.IsSuccess ? "success" : "failure",
        auditEvent.DurationMilliseconds,
        auditEvent.ErrorCode ?? "-");
}

internal sealed class AuditedGitGateway(IGitGateway inner, IBuckettieAuditLogger audit) : IGitGateway
{
    public Task<GitGatewayResult> GetStatusAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_repository_status", repository, () => inner.GetStatusAsync(repository, cancellationToken));

    public Task<GitGatewayResult> FetchAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_fetch", repository, () => inner.FetchAsync(repository, cancellationToken));

    public Task<GitGatewayResult> PullAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_pull", repository, () => inner.PullAsync(repository, cancellationToken));

    public Task<GitGatewayResult> PushAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_push", repository, () => inner.PushAsync(repository, cancellationToken));

    private async Task<GitGatewayResult> RunAsync(string tool, string repository, Func<Task<GitGatewayResult>> operation)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        GitGatewayResult result = await operation().ConfigureAwait(false);
        audit.Write(new(tool, repository, result.Branch, null, null, result.IsSuccess,
            stopwatch.ElapsedMilliseconds, result.Error?.ToString()));
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

    public Task<BitbucketResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(string repository, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_tag_list", repository, null, null, null, () => inner.ListTagsAsync(repository, cancellationToken));

    public Task<BitbucketResult<BitbucketTagInfo>> GetTagAsync(string repository, string tag, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_tag_get", repository, null, null, tag, () => inner.GetTagAsync(repository, tag, cancellationToken));

    public Task<BitbucketResult<BitbucketTagInfo>> CreateTagAsync(string repository, BitbucketTagCreate input, CancellationToken cancellationToken = default) =>
        RunAsync("bitbucket_tag_create", repository, null, null, input.Name, () => inner.CreateTagAsync(repository, input, cancellationToken));

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
        int? pullRequestId, string? tag, Func<Task<BitbucketResult<T>>> operation)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        BitbucketResult<T> result = await operation().ConfigureAwait(false);
        int? auditedPullRequestId = pullRequestId;
        if (auditedPullRequestId is null && result.Value is BitbucketPullRequestInfo pullRequest)
        {
            auditedPullRequestId = pullRequest.Id;
        }
        audit.Write(new(tool, repository, branch, auditedPullRequestId, tag, result.IsSuccess,
            stopwatch.ElapsedMilliseconds, result.Error?.ToString()));
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
