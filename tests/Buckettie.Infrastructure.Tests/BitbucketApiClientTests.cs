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

    [Fact]
    public async Task ListPullRequestsAsync_WhenStateIsSpecified_MapsPullRequests()
    {
        RecordingHandler handler = new(PullRequestPageJson());
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>> result = await client.ListPullRequestsAsync(
            "allowed",
            "workspace",
            "repository",
            BitbucketPullRequestState.Open,
            TestContext.Current.CancellationToken);

        result.Value.Should().ContainSingle().Which.Should().Match<BitbucketPullRequestInfo>(pullRequest =>
            pullRequest.Id == 7
            && pullRequest.SourceBranch == "develop"
            && pullRequest.DestinationBranch == "main");
        handler.Paths.Should().Equal(
            "repositories/workspace/repository/pullrequests?pagelen=100&page=1&state=OPEN");
    }

    [Fact]
    public async Task CreatePullRequestAsync_WhenCalled_SendsConfiguredRouteAndBody()
    {
        RecordingHandler handler = new(PullRequestJson());
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<BitbucketPullRequestInfo> result = await client.CreatePullRequestAsync(
            "allowed",
            "workspace",
            "repository",
            "develop",
            "main",
            new BitbucketPullRequestCreate("Release", "Description", true),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        handler.Methods.Should().Equal(HttpMethod.Post);
        handler.Paths.Should().Equal("repositories/workspace/repository/pullrequests");
        handler.Bodies.Should().ContainSingle().Which.Should().Contain("\"title\":\"Release\"")
            .And.Contain("\"source\":{\"branch\":{\"name\":\"develop\"}}")
            .And.Contain("\"destination\":{\"branch\":{\"name\":\"main\"}}")
            .And.Contain("\"draft\":true");
    }

    [Fact]
    public async Task MergePullRequestAsync_WhenConflict_ReturnsMergeConflict()
    {
        RecordingHandler handler = new(HttpStatusCode.Conflict);
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<BitbucketPullRequestInfo> result = await client.MergePullRequestAsync(
            "allowed",
            "workspace",
            "repository",
            7,
            new BitbucketPullRequestMerge(BitbucketMergeStrategy.Squash, "Merge release"),
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.PullRequestMergeConflict);
    }

    [Fact]
    public async Task GetPullRequestDiffAsync_WhenRedirectIsAllowed_ReturnsDiff()
    {
        RedirectHandler handler = new(
            new Uri("https://api.bitbucket.org/2.0/repositories/workspace/repository/diff/abc..def"),
            "diff --git a/file b/file");
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<string> result = await client.GetPullRequestDiffAsync(
            "allowed",
            "workspace",
            "repository",
            7,
            TestContext.Current.CancellationToken);

        result.Value.Should().Be("diff --git a/file b/file");
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPullRequestDiffAsync_WhenRedirectLeavesApiBoundary_RejectsRedirect()
    {
        RedirectHandler handler = new(new Uri("https://example.com/secret"), "not-used");
        BitbucketApiClient client = CreateClient(handler);

        BitbucketResult<string> result = await client.GetPullRequestDiffAsync(
            "allowed",
            "workspace",
            "repository",
            7,
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.InvalidResponse);
        handler.RequestCount.Should().Be(1);
    }

    private static BitbucketApiClient CreateClient(
        HttpMessageHandler handler,
        IApiTokenStore? tokenStore = null)
    {
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.bitbucket.org/2.0/") };
        return new BitbucketApiClient(
            httpClient,
            tokenStore ?? new StubTokenStore(ApiTokenStoreResult.Success("secret-token")),
            "developer@example.com");
    }

    private static string PullRequestPageJson() => $"{{\"values\":[{PullRequestJson()}]}}";

    private static string PullRequestJson() => """
        {"id":7,"title":"Release","description":"Description","state":"OPEN","source":{"branch":{"name":"develop"}},"destination":{"branch":{"name":"main"}},"draft":false,"links":{"html":{"href":"https://bitbucket.org/workspace/repository/pull-requests/7"}},"created_on":"2026-08-16T00:00:00Z","updated_on":"2026-08-16T01:00:00Z","merge_commit":null}
        """;

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

        internal List<HttpMethod> Methods { get; } = [];

        internal List<string> Bodies { get; } = [];

        internal string? AuthenticationScheme { get; private set; }

        internal string? AuthenticationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery.TrimStart('/').Replace("2.0/", string.Empty, StringComparison.Ordinal));
            Methods.Add(request.Method);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            AuthenticationScheme = request.Headers.Authorization?.Scheme;
            AuthenticationParameter = request.Headers.Authorization?.Parameter;
            (HttpStatusCode status, string content) = _responses.Dequeue();
            HttpResponseMessage response = new(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
            return response;
        }
    }

    private sealed class RedirectHandler(Uri location, string diff) : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1)
            {
                HttpResponseMessage redirect = new(HttpStatusCode.Redirect);
                redirect.Headers.Location = location;
                return Task.FromResult(redirect);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(diff, Encoding.UTF8, "text/plain"),
            });
        }
    }
}
