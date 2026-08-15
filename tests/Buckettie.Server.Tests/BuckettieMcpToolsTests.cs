using FluentAssertions;
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
}
