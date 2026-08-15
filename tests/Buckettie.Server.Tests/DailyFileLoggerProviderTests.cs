using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class DailyFileLoggerProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"buckettie-audit-{Guid.NewGuid():N}");

    [Fact]
    public void LoggerWritesStructuredAuditFields()
    {
        using DailyFileLoggerProvider provider = new(_directory);
        ILogger logger = provider.CreateLogger("Audit");
        logger.LogInformation("client={Client} tool={Tool} repository={Repository} result={Result} duration_ms={Duration}",
            "mcp", "bitbucket_branch_get", "example", "success", 12);

        string content = File.ReadAllText(Directory.GetFiles(_directory, "buckettie-*.log").Single());
        content.Should().Contain("tool=bitbucket_branch_get")
            .And.Contain("repository=example")
            .And.Contain("duration_ms=12")
            .And.NotContain("super-secret-token");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
