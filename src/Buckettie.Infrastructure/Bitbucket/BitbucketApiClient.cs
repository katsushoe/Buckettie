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
    private const int MaximumPages = 100;
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
        ApiTokenStoreResult token = _tokenStore.Read(repositoryId);
        if (!token.IsSuccess || token.Token is null)
        {
            return BitbucketResult<T>.Failure(BitbucketError.TokenUnavailable);
        }

        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        SetBasicAuthentication(request, token.Token);
        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return BitbucketResult<T>.Failure(MapStatusCode(response.StatusCode));
            }

            T? content = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            return content is null
                ? BitbucketResult<T>.Failure(BitbucketError.InvalidResponse)
                : BitbucketResult<T>.Success(content);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BitbucketResult<T>.Failure(BitbucketError.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return BitbucketResult<T>.Failure(BitbucketError.Timeout);
        }
        catch (HttpRequestException)
        {
            return BitbucketResult<T>.Failure(BitbucketError.NetworkError);
        }
        catch (System.Text.Json.JsonException)
        {
            return BitbucketResult<T>.Failure(BitbucketError.InvalidResponse);
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

    private static BitbucketBranchInfo MapBranch(BranchResponse branch) =>
        new(branch.Name!, branch.Target!.Hash!);

    private static bool IsValidRepository(RepositoryResponse repository) =>
        !string.IsNullOrWhiteSpace(repository.Uuid)
        && !string.IsNullOrWhiteSpace(repository.FullName)
        && !string.IsNullOrWhiteSpace(repository.Name);

    private static bool IsValidBranch(BranchResponse branch) =>
        !string.IsNullOrWhiteSpace(branch.Name)
        && !string.IsNullOrWhiteSpace(branch.Target?.Hash);

    private static BitbucketError MapStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => BitbucketError.AuthenticationFailed,
        HttpStatusCode.Forbidden => BitbucketError.PermissionDenied,
        HttpStatusCode.NotFound => BitbucketError.NotFound,
        HttpStatusCode.TooManyRequests => BitbucketError.RateLimited,
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

    private sealed record TargetResponse(string? Hash);
}
