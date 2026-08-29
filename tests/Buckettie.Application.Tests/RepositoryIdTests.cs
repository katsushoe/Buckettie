using Buckettie.Application.Repositories;
using FluentAssertions;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class RepositoryIdTests
{
    [Theory]
    [InlineData("buckettie")]
    [InlineData("cupperpro2")]
    public void IsValid_WhenIdMatchesProjectInboxRule_ReturnsTrue(string repositoryId)
    {
        RepositoryId.IsValid(repositoryId).Should().BeTrue();
    }

    [Theory]
    [InlineData("Buckettie")]
    [InlineData("ai_prompt")]
    [InlineData("obsidian-vault")]
    [InlineData("1repository")]
    public void IsValid_WhenIdDoesNotMatchProjectInboxRule_ReturnsFalse(string repositoryId)
    {
        RepositoryId.IsValid(repositoryId).Should().BeFalse();
    }

    [Theory]
    [InlineData("BUCKETTIE")]
    [InlineData("Buckettie2")]
    public void IsLookupValid_WhenIdDiffersOnlyByCase_ReturnsTrue(string repositoryId)
    {
        RepositoryId.IsLookupValid(repositoryId).Should().BeTrue();
    }
}
