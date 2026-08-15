using FluentAssertions;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;
using ModelContextProtocol.Server;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class BuckettieMcpToolsTests
{
    private static readonly string[] ExpectedToolNames =
    [
        "bitbucket_repository_status",
        "bitbucket_fetch",
        "bitbucket_pull",
        "bitbucket_push",
        "bitbucket_branch_list",
        "bitbucket_branch_get",
        "bitbucket_pr_list",
        "bitbucket_pr_get",
        "bitbucket_pr_diff",
        "bitbucket_pr_create",
        "bitbucket_pr_merge",
        "bitbucket_tag_list",
        "bitbucket_tag_get",
        "bitbucket_tag_create",
    ];

    [Fact]
    public void JsonOptions_WhenSdkMakesOptionsReadOnly_HasTypeInfoResolver()
    {
        Action act = () => BuckettieMcpJson.CreateOptions().MakeReadOnly();

        act.Should().NotThrow();
    }

    [Fact]
    public void ToolMethods_WhenInspected_ExposeExactlyThePhaseOneToolSet()
    {
        McpServerToolAttribute[] attributes = typeof(BuckettieMcpTools)
            .GetMethods()
            .Select(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
                .Cast<McpServerToolAttribute>()
                .SingleOrDefault())
            .Where(attribute => attribute is not null)
            .Cast<McpServerToolAttribute>()
            .ToArray();

        attributes.Select(attribute => attribute.Name).Should().BeEquivalentTo(ExpectedToolNames);
        attributes.Should().OnlyContain(attribute => attribute.UseStructuredContent);
    }

    [Fact]
    public void ToolMethods_WhenMutationIsImportant_MarkItDestructive()
    {
        string[] destructive = typeof(BuckettieMcpTools)
            .GetMethods()
            .Select(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
                .Cast<McpServerToolAttribute>()
                .SingleOrDefault())
            .Where(attribute => attribute?.Destructive == true)
            .Select(attribute => attribute!.Name!)
            .ToArray();

        destructive.Should().BeEquivalentTo(
            "bitbucket_push",
            "bitbucket_pr_create",
            "bitbucket_pr_merge",
            "bitbucket_tag_create");
    }

    [Fact]
    public void ToolMethods_WhenInspected_ReturnCommonStructuredResult()
    {
        Type[] returnTypes = typeof(BuckettieMcpTools)
            .GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false).Length == 1)
            .Select(method => method.ReturnType.GenericTypeArguments.Single())
            .ToArray();

        returnTypes.Should().OnlyContain(type =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BuckettieToolResult<>));
    }

    [Fact]
    public async Task MapGitAsync_WhenGatewayFails_ReturnsCommonErrorShape()
    {
        GitGatewayResult gatewayResult = GitGatewayResult.Failure(
            "push", "example", GitGatewayError.ProtectedBranch, "main");

        BuckettieToolResult<BuckettieGitData> result = await BuckettieToolResultMapper.MapGitAsync(
            Task.FromResult(gatewayResult));

        result.Should().Be(new BuckettieToolResult<BuckettieGitData>(
            false,
            "push",
            "example",
            null,
            new BuckettieToolError("protected_branch", "Direct push to the protected branch is not allowed.")));
    }

    [Theory]
    [InlineData("pr_get", "pull_request_not_found")]
    [InlineData("pr_diff", "pull_request_not_found")]
    [InlineData("pr_merge", "pull_request_not_found")]
    [InlineData("tag_get", "tag_not_found")]
    [InlineData("branch_get", "branch_not_found")]
    [InlineData("branch_list", "repository_not_found")]
    public void BitbucketCode_WhenApiReturnsNotFound_UsesOperationContext(string operation, string expected)
    {
        BuckettieToolResultMapper.BitbucketCode(BitbucketError.NotFound, operation).Should().Be(expected);
    }
}
