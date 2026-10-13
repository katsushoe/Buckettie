using System.IO.Pipes;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace Buckettie.ApprovalPrompt.Tests;

public sealed class PromptExceptionHandlingTests
{
    [Fact]
    public void Report_WhenInteractive_WritesDumpBeforeShowingSafeDialog()
    {
        List<string> calls = [];
        PromptExceptionReporter reporter = new(
            exception =>
            {
                calls.Add($"log:{exception.GetType().Name}");
                return "C:\\Users\\example\\AppData\\Local\\Akatsukisoft\\Buckettie\\logs\\prompt.log";
            },
            message => calls.Add($"dialog:{message}"),
            () => true);

        reporter.Report(new InvalidOperationException("secret-token-value"));

        calls.Should().HaveCount(2);
        calls[0].Should().Be("log:InvalidOperationException");
        calls[1].Should().Contain("dialog:").And.Contain("prompt.log").And.NotContain("secret-token-value");
    }

    [Fact]
    public void Report_WhenNoDesktop_WritesDumpWithoutAttemptingDialog()
    {
        bool logWritten = false;
        bool dialogShown = false;
        PromptExceptionReporter reporter = new(
            _ => { logWritten = true; return "prompt.log"; },
            _ => dialogShown = true,
            () => false);

        reporter.Report(new InvalidOperationException("startup failure"));

        logWritten.Should().BeTrue();
        dialogShown.Should().BeFalse();
    }

    [Fact]
    public void Report_WhenLogWriteFails_ShowsDialogWithFallbackMessage()
    {
        string? message = null;
        PromptExceptionReporter reporter = new(
            _ => throw new IOException("disk unavailable"),
            value => message = value,
            () => true);

        reporter.Report(new InvalidOperationException("startup failure"));

        message.Should().Be(PromptExceptionReporter.CreateMessage(
            null, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
        message.Should().NotContain("disk unavailable");
    }

    [Fact]
    public void Write_WhenExceptionContainsCredentialPatterns_RedactsSecretsAndKeepsStackDump()
    {
        using TemporaryDirectory directory = new();
        InvalidOperationException exception = new(
            "Authorization: Bearer abc.def.ghi; token=hidden-value; " +
            "\"api_token\":\"json-secret\"; client_secret=client-secret-value; " +
            "remote https://user:password@bitbucket.org/repo");

        string path = PromptExceptionDumpWriter.Write(exception, directory.Path);
        string dump = File.ReadAllText(path);

        dump.Should().Contain(nameof(InvalidOperationException));
        dump.Should().Contain("(redacted)");
        dump.Should().NotContain("abc.def.ghi");
        dump.Should().NotContain("hidden-value");
        dump.Should().NotContain("json-secret");
        dump.Should().NotContain("client-secret-value");
        dump.Should().NotContain("user:password");
    }

    [Fact]
    public async Task RunBoundary_WhenStartupThrows_ReportsAndReturnsFailureCode()
    {
        Exception? reported = null;

        int exitCode = await PromptExecutionBoundary.RunAsync(
            () => Task.FromException<int>(new InvalidOperationException("form initialization failed")),
            exception => reported = exception);

        exitCode.Should().Be(PromptExecutionBoundary.UnhandledExceptionExitCode);
        reported.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ConnectAsync_WhenPipeIsUnavailable_ReturnsFalse()
    {
        await using NamedPipeClientStream pipe = new(
            ".", $"Buckettie-test-{Guid.NewGuid():N}", PipeDirection.InOut, PipeOptions.Asynchronous);

        bool connected = await Program.ConnectAsync(pipe, TimeSpan.FromMilliseconds(50));

        connected.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ExecutesDialogActionOnStaThread()
    {
        ApartmentState state = await PromptStaThread.RunAsync(() => Thread.CurrentThread.GetApartmentState());

        state.Should().Be(ApartmentState.STA);
    }

    [Theory]
    [InlineData(DialogResult.Yes, false, true, "token-value")]
    [InlineData(DialogResult.No, false, false, null)]
    [InlineData(DialogResult.No, true, false, null)]
    public void DialogResult_FailsClosedForDenialAndTimeout(
        DialogResult dialogResult, bool timedOut, bool expectedApproved, string? expectedToken)
    {
        PromptDialogResult result = PromptDialogResult.From(dialogResult, timedOut, "token-value");

        result.Approved.Should().Be(expectedApproved);
        result.Token.Should().Be(expectedToken);
        result.TimedOut.Should().Be(timedOut);
    }

    [Fact]
    public void DialogResult_WithoutDesktop_DeniesAndReturnsNoToken()
    {
        PromptDialogResult result = PromptDialogResult.WithoutDesktop;

        result.Approved.Should().BeFalse();
        result.Token.Should().BeNull();
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task DesktopGuard_WhenNoInteractiveDesktop_DoesNotStartDialog()
    {
        bool dialogStarted = false;
        PromptDialogResult result = await PromptDesktopGuard.RunAsync(
            userInteractive: false,
            showDialog: () =>
            {
                dialogStarted = true;
                return Task.FromResult(new PromptDialogResult(true, "token-value", false));
            },
            fallback: PromptDialogResult.WithoutDesktop);

        dialogStarted.Should().BeFalse();
        result.Should().Be(PromptDialogResult.WithoutDesktop);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"Buckettie-ApprovalPrompt-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
