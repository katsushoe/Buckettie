using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Credentials;

namespace Buckettie.Infrastructure.Bitbucket;

/// <summary>Bitbucket Cloud REST APIの固定読取操作を実行します。</summary>
public sealed class BitbucketApiClient : IBitbucketApiClient
{
    private const int PageLength = 100;
    private const int PullRequestPageLength = 50;
    private const int MaximumPages = 100;
    private const int MaximumDiffCharacters = 5_000_000;
    private const int MergeTaskPollAttempts = 3;
    private static readonly TimeSpan MergeTaskPollDelay = TimeSpan.FromMilliseconds(250);
    private readonly HttpClient _httpClient;
    private readonly IApiTokenStore _tokenStore;
    private readonly string _atlassianEmail;

    /// <summary>REST Clientを初期化します。</summary>
    public BitbucketApiClient(HttpClient httpClient, IApiTokenStore tokenStore, string atlassianEmail)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokenStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(atlassianEmail);
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _atlassianEmail = atlassianEmail;
    }

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketRepositoryInfo>> GetRepositoryAsync(
        string repositoryId,
        string workspace,
        string slug,
        CancellationToken cancellationToken = default) =>
        GetAsync<RepositoryResponse, BitbucketRepositoryInfo>(
            repositoryId,
            RepositoryPath(workspace, slug),
            IsValidRepository,
            response => new(
                response.Uuid!,
                response.FullName!,
                response.Name!,
                response.IsPrivate,
                response.MainBranch?.Name),
            cancellationToken);

    /// <inheritdoc />
    public async Task<BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>> ListBranchesAsync(
        string repositoryId,
        string workspace,
        string slug,
        CancellationToken cancellationToken = default)
    {
        List<BitbucketBranchInfo> branches = [];
        string basePath = $"{RepositoryPath(workspace, slug)}/refs/branches";
        for (int page = 1; page <= MaximumPages; page++)
        {
            BitbucketResult<BranchPageResponse> result = await GetResponseAsync<BranchPageResponse>(
                repositoryId,
                $"{basePath}?pagelen={PageLength}&page={page}",
                cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value is null)
            {
                return BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>.Failure(
                    result.Error ?? BitbucketError.InvalidResponse);
            }

            if (result.Value.Values is null || result.Value.Values.Any(branch => !IsValidBranch(branch)))
            {
                return BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>.Failure(BitbucketError.InvalidResponse);
            }

            branches.AddRange(result.Value.Values.Select(MapBranch));
            if (string.IsNullOrEmpty(result.Value.Next))
            {
                return BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>.Success(branches);
            }
        }

        return BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>.Failure(BitbucketError.InvalidResponse);
    }

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketBranchInfo>> GetBranchAsync(
        string repositoryId,
        string workspace,
        string slug,
        string branch,
        CancellationToken cancellationToken = default) =>
        GetAsync<BranchResponse, BitbucketBranchInfo>(
            repositoryId,
            $"{RepositoryPath(workspace, slug)}/refs/branches/{Uri.EscapeDataString(branch)}",
            IsValidBranch,
            MapBranch,
            cancellationToken);

    /// <inheritdoc />
    public async Task<BitbucketResult<BitbucketBranchInfo>> CreateBranchAsync(
        string repositoryId,
        string workspace,
        string slug,
        BitbucketBranchCreate input,
        CancellationToken cancellationToken = default)
    {
        BranchCreateRequest request = new(input.Name, new(input.TargetHash));
        BitbucketResult<BitbucketBranchInfo> result = await SendJsonAsync<
            BranchCreateRequest, BranchResponse, BitbucketBranchInfo>(
            repositoryId,
            HttpMethod.Post,
            $"{RepositoryPath(workspace, slug)}/refs/branches",
            request,
            IsValidBranch,
            MapBranch,
            cancellationToken).ConfigureAwait(false);
        return result.Error == BitbucketError.PullRequestMergeConflict
            ? BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.BranchAlreadyExists)
            : result;
    }

    /// <inheritdoc />
    public Task<BitbucketResult<bool>> DeleteBranchAsync(
        string repositoryId,
        string workspace,
        string slug,
        string branch,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(
            repositoryId,
            $"{RepositoryPath(workspace, slug)}/refs/branches/{Uri.EscapeDataString(branch)}",
            cancellationToken);

    /// <inheritdoc />
    public async Task<BitbucketResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(
        string repositoryId,
        string workspace,
        string slug,
        CancellationToken cancellationToken = default)
    {
        List<BitbucketTagInfo> tags = [];
        string basePath = $"{RepositoryPath(workspace, slug)}/refs/tags";
        for (int page = 1; page <= MaximumPages; page++)
        {
            BitbucketResult<TagPageResponse> result = await GetResponseAsync<TagPageResponse>(
                repositoryId,
                $"{basePath}?pagelen={PageLength}&page={page}",
                cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value?.Values is null)
            {
                return BitbucketResult<IReadOnlyList<BitbucketTagInfo>>.Failure(
                    result.Error ?? BitbucketError.InvalidResponse);
            }

            if (result.Value.Values.Any(tag => !IsValidTag(tag)))
            {
                return BitbucketResult<IReadOnlyList<BitbucketTagInfo>>.Failure(BitbucketError.InvalidResponse);
            }

            tags.AddRange(result.Value.Values.Select(MapTag));
            if (string.IsNullOrEmpty(result.Value.Next))
            {
                return BitbucketResult<IReadOnlyList<BitbucketTagInfo>>.Success(tags);
            }
        }

        return BitbucketResult<IReadOnlyList<BitbucketTagInfo>>.Failure(BitbucketError.InvalidResponse);
    }

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketTagInfo>> GetTagAsync(
        string repositoryId,
        string workspace,
        string slug,
        string tag,
        CancellationToken cancellationToken = default) =>
        GetAsync<TagResponse, BitbucketTagInfo>(
            repositoryId,
            $"{RepositoryPath(workspace, slug)}/refs/tags/{Uri.EscapeDataString(tag)}",
            IsValidTag,
            MapTag,
            cancellationToken);

    /// <inheritdoc />
    public async Task<BitbucketResult<BitbucketTagInfo>> CreateTagAsync(
        string repositoryId,
        string workspace,
        string slug,
        string targetHash,
        BitbucketTagCreate input,
        CancellationToken cancellationToken = default)
    {
        TagCreateRequest request = new(input.Name, new(targetHash), input.Message);
        BitbucketResult<BitbucketTagInfo> result = await SendJsonAsync<TagCreateRequest, TagResponse, BitbucketTagInfo>(
            repositoryId,
            HttpMethod.Post,
            $"{RepositoryPath(workspace, slug)}/refs/tags",
            request,
            IsValidTag,
            MapTag,
            cancellationToken).ConfigureAwait(false);
        return result.Error == BitbucketError.PullRequestMergeConflict
            ? BitbucketResult<BitbucketTagInfo>.Failure(BitbucketError.TagAlreadyExists)
            : result;
    }

    /// <inheritdoc />
    public Task<BitbucketResult<bool>> DeleteTagAsync(
        string repositoryId,
        string workspace,
        string slug,
        string tag,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(
            repositoryId,
            $"{RepositoryPath(workspace, slug)}/refs/tags/{Uri.EscapeDataString(tag)}",
            cancellationToken);

    /// <inheritdoc />
    public async Task<BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(
        string repositoryId,
        string workspace,
        string slug,
        BitbucketPullRequestState? state,
        CancellationToken cancellationToken = default)
    {
        List<BitbucketPullRequestInfo> pullRequests = [];
        string stateQuery = state is null ? string.Empty : $"&state={StateName(state.Value)}";
        string basePath = $"{RepositoryPath(workspace, slug)}/pullrequests";
        for (int page = 1; page <= MaximumPages; page++)
        {
            BitbucketResult<PullRequestPageResponse> result = await GetResponseAsync<PullRequestPageResponse>(
                repositoryId,
                $"{basePath}?pagelen={PullRequestPageLength}&page={page}{stateQuery}",
                cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value?.Values is null)
            {
                return BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Failure(
                    result.Error ?? BitbucketError.InvalidResponse);
            }

            if (result.Value.Values.Any(pullRequest => !IsValidPullRequest(pullRequest)))
            {
                return BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Failure(
                    BitbucketError.InvalidResponse);
            }

            pullRequests.AddRange(result.Value.Values.Select(MapPullRequest));
            if (string.IsNullOrEmpty(result.Value.Next))
            {
                return BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Success(pullRequests);
            }
        }

        return BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Failure(BitbucketError.InvalidResponse);
    }

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketPullRequestInfo>> GetPullRequestAsync(
        string repositoryId,
        string workspace,
        string slug,
        int pullRequestId,
        CancellationToken cancellationToken = default) =>
        GetAsync<PullRequestResponse, BitbucketPullRequestInfo>(
            repositoryId,
            PullRequestPath(workspace, slug, pullRequestId),
            IsValidPullRequest,
            MapPullRequest,
            cancellationToken);

    /// <inheritdoc />
    public async Task<BitbucketResult<string>> GetPullRequestDiffAsync(
        string repositoryId,
        string workspace,
        string slug,
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        string path = $"{PullRequestPath(workspace, slug, pullRequestId)}/diff";
        BitbucketResult<HttpResponseMessage> initial = await SendAsync(
            repositoryId,
            new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken).ConfigureAwait(false);
        if (!initial.IsSuccess || initial.Value is null)
        {
            return BitbucketResult<string>.Failure(initial.Error ?? BitbucketError.ApiError);
        }

        using HttpResponseMessage initialResponse = initial.Value;
        if (initialResponse.StatusCode != HttpStatusCode.Redirect || initialResponse.Headers.Location is null
            || !IsAllowedDiffLocation(initialResponse.Headers.Location, workspace, slug))
        {
            return BitbucketResult<string>.Failure(BitbucketError.InvalidResponse);
        }

        BitbucketResult<HttpResponseMessage> redirected = await SendAsync(
            repositoryId,
            new HttpRequestMessage(HttpMethod.Get, initialResponse.Headers.Location),
            cancellationToken).ConfigureAwait(false);
        if (!redirected.IsSuccess || redirected.Value is null)
        {
            return BitbucketResult<string>.Failure(redirected.Error ?? BitbucketError.ApiError);
        }

        using HttpResponseMessage response = redirected.Value;
        return await ReadBoundedTextAsync(response.Content, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<BitbucketResult<BitbucketPullRequestInfo>> CreatePullRequestAsync(
        string repositoryId,
        string workspace,
        string slug,
        string sourceBranch,
        string destinationBranch,
        BitbucketPullRequestCreate input,
        CancellationToken cancellationToken = default)
    {
        PullRequestCreateRequest request = new(
            input.Title,
            input.Description,
            input.Draft,
            new(new(sourceBranch)),
            new(new(destinationBranch)));
        return SendJsonAsync<PullRequestCreateRequest, PullRequestResponse, BitbucketPullRequestInfo>(
            repositoryId,
            HttpMethod.Post,
            $"{RepositoryPath(workspace, slug)}/pullrequests",
            request,
            IsValidPullRequest,
            MapPullRequest,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BitbucketResult<BitbucketPullRequestInfo>> MergePullRequestAsync(
        string repositoryId,
        string workspace,
        string slug,
        int pullRequestId,
        BitbucketPullRequestMerge input,
        CancellationToken cancellationToken = default)
    {
        PullRequestMergeRequest request = new(
            input.Message,
            MergeStrategyName(input.Strategy),
            false);
        string mergePath = $"{PullRequestPath(workspace, slug, pullRequestId)}/merge";
        using HttpRequestMessage message = new(HttpMethod.Post, $"{mergePath}?async=true")
        {
            Content = JsonContent.Create(request),
        };
        BitbucketResult<HttpResponseMessage> sent = await SendAsync(
            repositoryId,
            message,
            cancellationToken,
            HttpStatusCode.Conflict).ConfigureAwait(false);
        if (!sent.IsSuccess || sent.Value is null)
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(sent.Error ?? BitbucketError.ApiError);
        }

        using HttpResponseMessage response = sent.Value;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(
                await ClassifyMergeFailureAsync(response.Content, cancellationToken).ConfigureAwait(false));
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return await ReadPullRequestAsync(response.Content, cancellationToken).ConfigureAwait(false);
        }

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.MergeabilityUnknown);
        }

        MergeTaskResponse? task = await ReadJsonAsync<MergeTaskResponse>(
            response.Content,
            cancellationToken).ConfigureAwait(false);
        if (task is null || !TryGetMergeTaskId(task, workspace, slug, pullRequestId, out string taskId))
        {
            return BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.MergeabilityUnknown);
        }

        for (int attempt = 0; attempt < MergeTaskPollAttempts; attempt++)
        {
            BitbucketResult<BitbucketPullRequestInfo>? completed = MapMergeTask(task);
            if (completed is not null)
            {
                return completed;
            }

            await Task.Delay(MergeTaskPollDelay, cancellationToken).ConfigureAwait(false);
            using HttpRequestMessage pollRequest = new(
                HttpMethod.Get,
                $"{mergePath}/task-status/{Uri.EscapeDataString(taskId)}");
            BitbucketResult<HttpResponseMessage> polled = await SendAsync(
                repositoryId,
                pollRequest,
                cancellationToken,
                HttpStatusCode.Conflict).ConfigureAwait(false);
            if (!polled.IsSuccess || polled.Value is null)
            {
                return BitbucketResult<BitbucketPullRequestInfo>.Failure(
                    polled.Error ?? BitbucketError.MergeabilityUnknown);
            }

            using HttpResponseMessage pollResponse = polled.Value;
            if (pollResponse.StatusCode == HttpStatusCode.Conflict)
            {
                return BitbucketResult<BitbucketPullRequestInfo>.Failure(
                    await ClassifyMergeFailureAsync(pollResponse.Content, cancellationToken).ConfigureAwait(false));
            }

            task = await ReadJsonAsync<MergeTaskResponse>(
                pollResponse.Content,
                cancellationToken).ConfigureAwait(false);
            if (task is null)
            {
                return BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.MergeabilityUnknown);
            }
        }

        return BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.MergeabilityCalculating);
    }

    private static BitbucketResult<BitbucketPullRequestInfo>? MapMergeTask(MergeTaskResponse task)
    {
        if (string.Equals(task.TaskStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(task.TaskStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            return task.MergeResult is not null && IsValidPullRequest(task.MergeResult)
                ? BitbucketResult<BitbucketPullRequestInfo>.Success(
                    MapPullRequest(task.MergeResult) with { MergeabilityStatus = "mergeable" })
                : BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.MergeabilityUnknown);
        }

        return BitbucketResult<BitbucketPullRequestInfo>.Failure(
            ClassifyMergeFailure(task.Error?.Message));
    }

    private static async Task<BitbucketResult<BitbucketPullRequestInfo>> ReadPullRequestAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        PullRequestResponse? pullRequest = await ReadJsonAsync<PullRequestResponse>(
            content,
            cancellationToken).ConfigureAwait(false);
        return pullRequest is not null && IsValidPullRequest(pullRequest)
            ? BitbucketResult<BitbucketPullRequestInfo>.Success(
                MapPullRequest(pullRequest) with { MergeabilityStatus = "mergeable" })
            : BitbucketResult<BitbucketPullRequestInfo>.Failure(BitbucketError.MergeabilityUnknown);
    }

    private static async Task<BitbucketError> ClassifyMergeFailureAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        ProviderErrorResponse? error = await ReadJsonAsync<ProviderErrorResponse>(
            content,
            cancellationToken).ConfigureAwait(false);
        return ClassifyMergeFailure(error?.Error?.Message);
    }

    private static BitbucketError ClassifyMergeFailure(string? message)
    {
        if (message?.Contains("conflict", StringComparison.OrdinalIgnoreCase) == true)
        {
            return BitbucketError.PullRequestMergeConflict;
        }

        if (ContainsAny(message, "approval", "build", "check", "branch restriction", "blocked", "not permitted"))
        {
            return BitbucketError.PullRequestMergeBlocked;
        }

        return BitbucketError.MergeabilityUnknown;
    }

    private static bool ContainsAny(string? value, params string[] patterns) =>
        value is not null && patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetMergeTaskId(
        MergeTaskResponse task,
        string workspace,
        string slug,
        int pullRequestId,
        out string taskId)
    {
        taskId = string.Empty;
        if (!Uri.TryCreate(task.Links?.Self?.Href, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "api.bitbucket.org", StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort)
        {
            return false;
        }

        string prefix = $"/2.0/{PullRequestPath(workspace, slug, pullRequestId)}/merge/task-status/";
        if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]);
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('/', StringComparison.Ordinal)
            || candidate.Any(char.IsControl))
        {
            return false;
        }

        taskId = candidate;
        return true;
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            return await content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            return default;
        }
    }

    private async Task<BitbucketResult<TOutput>> GetAsync<TResponse, TOutput>(
        string repositoryId,
        string path,
        Func<TResponse, bool> validate,
        Func<TResponse, TOutput> map,
        CancellationToken cancellationToken)
    {
        BitbucketResult<TResponse> result = await GetResponseAsync<TResponse>(
            repositoryId,
            path,
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess && result.Value is not null && validate(result.Value)
            ? BitbucketResult<TOutput>.Success(map(result.Value))
            : BitbucketResult<TOutput>.Failure(result.Error ?? BitbucketError.InvalidResponse);
    }

    private async Task<BitbucketResult<T>> GetResponseAsync<T>(
        string repositoryId,
        string path,
        CancellationToken cancellationToken)
    {
        BitbucketResult<HttpResponseMessage> sent = await SendAsync(
            repositoryId,
            new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken).ConfigureAwait(false);
        if (!sent.IsSuccess || sent.Value is null)
        {
            return BitbucketResult<T>.Failure(sent.Error ?? BitbucketError.ApiError);
        }

        using HttpResponseMessage response = sent.Value;
        try
        {
            T? content = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            return content is null
                ? BitbucketResult<T>.Failure(BitbucketError.InvalidResponse)
                : BitbucketResult<T>.Success(content);
        }
        catch (System.Text.Json.JsonException)
        {
            return BitbucketResult<T>.Failure(BitbucketError.InvalidResponse);
        }
    }

    private async Task<BitbucketResult<TOutput>> SendJsonAsync<TRequest, TResponse, TOutput>(
        string repositoryId,
        HttpMethod method,
        string path,
        TRequest body,
        Func<TResponse, bool> validate,
        Func<TResponse, TOutput> map,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method, path)
        {
            Content = JsonContent.Create(body),
        };
        BitbucketResult<HttpResponseMessage> sent = await SendAsync(
            repositoryId,
            request,
            cancellationToken).ConfigureAwait(false);
        if (!sent.IsSuccess || sent.Value is null)
        {
            return BitbucketResult<TOutput>.Failure(sent.Error ?? BitbucketError.ApiError);
        }

        using HttpResponseMessage response = sent.Value;
        try
        {
            TResponse? content = await response.Content.ReadFromJsonAsync<TResponse>(
                cancellationToken).ConfigureAwait(false);
            return content is not null && validate(content)
                ? BitbucketResult<TOutput>.Success(map(content))
                : BitbucketResult<TOutput>.Failure(BitbucketError.InvalidResponse);
        }
        catch (System.Text.Json.JsonException)
        {
            return BitbucketResult<TOutput>.Failure(BitbucketError.InvalidResponse);
        }
    }

    private async Task<BitbucketResult<bool>> DeleteAsync(
        string repositoryId,
        string path,
        CancellationToken cancellationToken)
    {
        BitbucketResult<HttpResponseMessage> sent = await SendAsync(
            repositoryId,
            new HttpRequestMessage(HttpMethod.Delete, path),
            cancellationToken).ConfigureAwait(false);
        if (!sent.IsSuccess || sent.Value is null)
        {
            return BitbucketResult<bool>.Failure(sent.Error ?? BitbucketError.ApiError);
        }

        using HttpResponseMessage response = sent.Value;
        return BitbucketResult<bool>.Success(true);
    }

    private async Task<BitbucketResult<HttpResponseMessage>> SendAsync(
        string repositoryId,
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        HttpStatusCode? acceptedStatus = null)
    {
        ApiTokenStoreResult token = _tokenStore.Read(repositoryId);
        if (!token.IsSuccess || token.Token is null)
        {
            request.Dispose();
            return BitbucketResult<HttpResponseMessage>.Failure(BitbucketError.TokenUnavailable);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        SetBasicAuthentication(request, token.Token);
        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect
                || response.StatusCode == acceptedStatus)
            {
                return BitbucketResult<HttpResponseMessage>.Success(response);
            }

            BitbucketError error = MapStatusCode(response.StatusCode);
            response.Dispose();
            return BitbucketResult<HttpResponseMessage>.Failure(error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BitbucketResult<HttpResponseMessage>.Failure(BitbucketError.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return BitbucketResult<HttpResponseMessage>.Failure(BitbucketError.Timeout);
        }
        catch (HttpRequestException)
        {
            return BitbucketResult<HttpResponseMessage>.Failure(BitbucketError.NetworkError);
        }
        finally
        {
            request.Dispose();
        }
    }

    private static async Task<BitbucketResult<string>> ReadBoundedTextAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumDiffCharacters)
        {
            return BitbucketResult<string>.Failure(BitbucketError.InvalidResponse);
        }

        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        StringBuilder text = new();
        char[] buffer = new char[8192];
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return BitbucketResult<string>.Success(text.ToString());
            }

            if (text.Length + read > MaximumDiffCharacters)
            {
                return BitbucketResult<string>.Failure(BitbucketError.InvalidResponse);
            }

            text.Append(buffer, 0, read);
        }
    }

    private void SetBasicAuthentication(HttpRequestMessage request, string token)
    {
        byte[] credentials = Encoding.UTF8.GetBytes($"{_atlassianEmail}:{token}");
        try
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(credentials));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentials);
        }
    }

    private static string RepositoryPath(string workspace, string slug) =>
        $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(slug)}";

    private static string PullRequestPath(string workspace, string slug, int pullRequestId) =>
        $"{RepositoryPath(workspace, slug)}/pullrequests/{pullRequestId}";

    private static bool IsAllowedDiffLocation(Uri location, string workspace, string slug)
    {
        if (!location.IsAbsoluteUri
            || !string.Equals(location.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(location.Host, "api.bitbucket.org", StringComparison.OrdinalIgnoreCase)
            || !location.IsDefaultPort)
        {
            return false;
        }

        string expectedPrefix = $"/2.0/{RepositoryPath(workspace, slug)}/diff/";
        return location.AbsolutePath.StartsWith(expectedPrefix, StringComparison.Ordinal);
    }

    private static BitbucketBranchInfo MapBranch(BranchResponse branch) =>
        new(branch.Name!, branch.Target!.Hash!);

    private static bool IsValidRepository(RepositoryResponse repository) =>
        !string.IsNullOrWhiteSpace(repository.Uuid)
        && !string.IsNullOrWhiteSpace(repository.FullName)
        && !string.IsNullOrWhiteSpace(repository.Name);

    private static bool IsValidBranch(BranchResponse branch) =>
        !string.IsNullOrWhiteSpace(branch.Name)
        && !string.IsNullOrWhiteSpace(branch.Target?.Hash);

    private static BitbucketTagInfo MapTag(TagResponse tag) => new(
        tag.Name!,
        tag.Target!.Hash!,
        tag.Message,
        tag.Date,
        tag.Tagger?.DisplayName);

    private static bool IsValidTag(TagResponse tag) =>
        !string.IsNullOrWhiteSpace(tag.Name)
        && !string.IsNullOrWhiteSpace(tag.Target?.Hash);

    private static BitbucketPullRequestInfo MapPullRequest(PullRequestResponse pullRequest) => new(
        pullRequest.Id,
        pullRequest.Title!,
        pullRequest.Description ?? string.Empty,
        pullRequest.State!,
        pullRequest.Source!.Branch!.Name!,
        pullRequest.Destination!.Branch!.Name!,
        pullRequest.Draft,
        pullRequest.Links?.Html?.Href,
        pullRequest.CreatedOn,
        pullRequest.UpdatedOn,
        pullRequest.MergeCommit?.Hash);

    private static bool IsValidPullRequest(PullRequestResponse pullRequest) =>
        pullRequest.Id > 0
        && !string.IsNullOrWhiteSpace(pullRequest.Title)
        && !string.IsNullOrWhiteSpace(pullRequest.State)
        && !string.IsNullOrWhiteSpace(pullRequest.Source?.Branch?.Name)
        && !string.IsNullOrWhiteSpace(pullRequest.Destination?.Branch?.Name)
        && pullRequest.CreatedOn != default
        && pullRequest.UpdatedOn != default;

    private static string StateName(BitbucketPullRequestState state) => state switch
    {
        BitbucketPullRequestState.Open => "OPEN",
        BitbucketPullRequestState.Merged => "MERGED",
        BitbucketPullRequestState.Declined => "DECLINED",
        BitbucketPullRequestState.Superseded => "SUPERSEDED",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string? MergeStrategyName(BitbucketMergeStrategy strategy) => strategy switch
    {
        BitbucketMergeStrategy.RepositoryDefault => null,
        BitbucketMergeStrategy.MergeCommit => "merge_commit",
        BitbucketMergeStrategy.Squash => "squash",
        BitbucketMergeStrategy.FastForward => "fast_forward",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    private static BitbucketError MapStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => BitbucketError.AuthenticationFailed,
        HttpStatusCode.Forbidden => BitbucketError.PermissionDenied,
        HttpStatusCode.NotFound => BitbucketError.NotFound,
        HttpStatusCode.TooManyRequests => BitbucketError.RateLimited,
        HttpStatusCode.Conflict => BitbucketError.PullRequestMergeConflict,
        _ => BitbucketError.ApiError,
    };

    private sealed record RepositoryResponse(
        string? Uuid,
        [property: JsonPropertyName("full_name")] string? FullName,
        string? Name,
        [property: JsonPropertyName("is_private")] bool IsPrivate,
        [property: JsonPropertyName("mainbranch")] NamedResponse? MainBranch);

    private sealed record NamedResponse(string? Name);

    private sealed record BranchPageResponse(IReadOnlyList<BranchResponse>? Values, string? Next);

    private sealed record BranchResponse(string? Name, TargetResponse? Target);

    private sealed record BranchCreateRequest(string Name, TargetResponse Target);

    private sealed record TargetResponse(string? Hash);

    private sealed record TagPageResponse(IReadOnlyList<TagResponse>? Values, string? Next);

    private sealed record TagResponse(
        string? Name,
        TargetResponse? Target,
        string? Message,
        DateTimeOffset? Date,
        TaggerResponse? Tagger);

    private sealed record TaggerResponse(
        [property: JsonPropertyName("display_name")] string? DisplayName);

    private sealed record TagCreateRequest(string Name, TargetResponse Target, string? Message);

    private sealed record PullRequestPageResponse(IReadOnlyList<PullRequestResponse>? Values, string? Next);

    private sealed record PullRequestResponse(
        int Id,
        string? Title,
        string? Description,
        string? State,
        PullRequestSideResponse? Source,
        PullRequestSideResponse? Destination,
        bool Draft,
        LinkCollectionResponse? Links,
        [property: JsonPropertyName("created_on")] DateTimeOffset CreatedOn,
        [property: JsonPropertyName("updated_on")] DateTimeOffset UpdatedOn,
        [property: JsonPropertyName("merge_commit")] TargetResponse? MergeCommit);

    private sealed record PullRequestSideResponse(NamedResponse? Branch);

    private sealed record LinkCollectionResponse(LinkResponse? Html, LinkResponse? Self = null);

    private sealed record LinkResponse(string? Href);

    private sealed record PullRequestCreateRequest(
        string Title,
        string Description,
        bool Draft,
        PullRequestSideRequest Source,
        PullRequestSideRequest Destination);

    private sealed record PullRequestSideRequest(NamedRequest Branch);

    private sealed record NamedRequest(string Name);

    private sealed record PullRequestMergeRequest(
        string? Message,
        [property: JsonPropertyName("merge_strategy")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MergeStrategy,
        [property: JsonPropertyName("close_source_branch")] bool CloseSourceBranch);

    private sealed record MergeTaskResponse(
        [property: JsonPropertyName("task_status")] string? TaskStatus,
        [property: JsonPropertyName("merge_result")] PullRequestResponse? MergeResult,
        LinkCollectionResponse? Links,
        ErrorMessageResponse? Error);

    private sealed record ProviderErrorResponse(ErrorMessageResponse? Error);

    private sealed record ErrorMessageResponse(string? Message);
}
