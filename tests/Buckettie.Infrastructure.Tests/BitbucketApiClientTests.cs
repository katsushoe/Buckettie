using System.Net;
using System.Text;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Credentials;
using Buckettie.Infrastructure.Bitbucket;
using FluentAssertions;
using Xunit;

namespace Buckettie.Infrastructure.Tests;

public sealed class BitbucketApiClientTests
{
    [Fact]
    public async Task GetRepositoryAsync_WhenResponseIsValid_ReturnsRepositoryAndUsesBasicAuthentication()
    {
        RecordingHandler handler = new("""
            {"uuid":"{uuid}","full_name":"workspace/repository","name":"Repository","is_private":true,"mainbranch":{"name":"main"}}
            """);
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<BitbucketRepositoryInfo> result = await client.GetRepositoryAsync(
            "allowed",
            "workspace",
            "repository",
            TestContext.Current.CancellationToken);

        result.Value.Should().Be(new BitbucketRepositoryInfo(
            "{uuid}", "workspace/repository", "Repository", true, "main"));
        handler.Paths.Should().Equal("repositories/workspace/repository");
        handler.AuthenticationScheme.Should().Be("Basic");
        handler.AuthenticationParameter.Should().Be(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("developer@example.com:secret-token")));
    }

    [Fact]
    public async Task ListBranchesAsync_WhenResponseHasNext_ReadsAllPages()
    {
        RecordingHandler handler = new(
            "{\"values\":[{\"name\":\"develop\",\"target\":{\"hash\":\"abc\"}}],\"next\":\"present\"}",
            "{\"values\":[{\"name\":\"main\",\"target\":{\"hash\":\"def\"}}]}");
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<IReadOnlyList<BitbucketBranchInfo>> result = await client.ListBranchesAsync(
            "allowed",
            "workspace",
            "repository",
            TestContext.Current.CancellationToken);

        result.Value.Should().Equal(
            new BitbucketBranchInfo("develop", "abc"),
            new BitbucketBranchInfo("main", "def"));
        handler.Paths.Should().Equal(
            "repositories/workspace/repository/refs/branches?pagelen=100&page=1",
            "repositories/workspace/repository/refs/branches?pagelen=100&page=2");
    }

    [Fact]
    public async Task GetBranchAsync_WhenUnauthorized_ReturnsAuthenticationError()
    {
        RecordingHandler handler = new(HttpStatusCode.Unauthorized);
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<BitbucketBranchInfo> result = await client.GetBranchAsync(
            "allowed",
            "workspace",
            "repository",
            "feature/test",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.AuthenticationFailed);
        handler.Paths.Should().Equal("repositories/workspace/repository/refs/branches/feature%2Ftest");
    }

    [Fact]
    public async Task GetRepositoryAsync_WhenTokenIsUnavailable_DoesNotSendRequest()
    {
        RecordingHandler handler = new("{}");
        StubTokenStore tokenStore = new(ApiTokenStoreResult.Failure(ApiTokenStoreError.TokenNotFound));
        BitbucketApiClient client = CreateClient(handler, tokenStore);

        BitbucketResult<BitbucketRepositoryInfo> result = await client.GetRepositoryAsync(
            "allowed",
            "workspace",
            "repository",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.TokenUnavailable);
        handler.Paths.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRepositoryAsync_WhenRequiredJsonIsMissing_ReturnsInvalidResponse()
    {
        RecordingHandler handler = new("{\"uuid\":\"{uuid}\"}");
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<BitbucketRepositoryInfo> result = await client.GetRepositoryAsync(
            "allowed",
            "workspace",
            "repository",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.InvalidResponse);
    }

    private static BitbucketApiClient CreateClient(
        RecordingHandler handler,
        IApiTokenStore? tokenStore = null)
    {
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.bitbucket.org/2.0/") };
        return new BitbucketApiClient(
            httpClient,
            tokenStore ?? new StubTokenStore(ApiTokenStoreResult.Success("secret-token")),
            "developer@example.com");
    }

    private sealed class StubTokenStore(ApiTokenStoreResult result) : IApiTokenStore
    {
        public ApiTokenStoreResult Save(string repositoryId, string token) => throw new NotSupportedException();

        public ApiTokenStoreResult Read(string repositoryId) => result;

        public ApiTokenStoreResult Delete(string repositoryId) => throw new NotSupportedException();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Content)> _responses;

        internal RecordingHandler(params string[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(
                responses.Select(content => (HttpStatusCode.OK, content)));
        }

        internal RecordingHandler(HttpStatusCode statusCode)
        {
            _responses = new Queue<(HttpStatusCode, string)>([(statusCode, string.Empty)]);
        }

        internal List<string> Paths { get; } = [];

        internal string? AuthenticationScheme { get; private set; }

        internal string? AuthenticationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery.TrimStart('/').Replace("2.0/", string.Empty, StringComparison.Ordinal));
            AuthenticationScheme = request.Headers.Authorization?.Scheme;
            AuthenticationParameter = request.Headers.Authorization?.Parameter;
            (HttpStatusCode status, string content) = _responses.Dequeue();
            HttpResponseMessage response = new(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
