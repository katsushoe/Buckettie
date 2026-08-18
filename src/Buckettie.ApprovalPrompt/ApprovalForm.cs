using System.Drawing;
using System.Windows.Forms;
using Buckettie.Application.Interactive;

namespace Buckettie.ApprovalPrompt;

/// <summary>
/// Repository登録候補を提示し、Approve／Denyを人間に選ばせるDialogです。
/// Timeoutでは自動的にDenyへfail closedします。
/// </summary>
internal sealed class ApprovalForm : Form
{
    private const int TimeoutSeconds = 120;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private readonly Label _countdownLabel;
    private int _remainingSeconds = TimeoutSeconds;

    /// <summary>承認Dialogを構築します。</summary>
    public ApprovalForm(ApprovalPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Text = "Buckettie - Repository Registration Approval";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = true;
        ClientSize = new Size(480, 300);

        AddField("Repository ID", request.RepositoryId, 16);
        AddField("Workspace", request.Workspace, 56);
        AddField("Slug", request.Slug, 96);
        AddField("Local Root", request.LocalRoot, 136);
        AddField("Remote URL", request.RemoteUrl, 176);

        _countdownLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 220),
            Text = FormatCountdown(_remainingSeconds),
        };
        Controls.Add(_countdownLabel);

        Button approveButton = new()
        {
            Text = "&Approve",
            DialogResult = DialogResult.Yes,
            Location = new Point(232, 248),
            Size = new Size(110, 32),
        };
        Button denyButton = new()
        {
            Text = "&Deny",
            DialogResult = DialogResult.No,
            Location = new Point(352, 248),
            Size = new Size(110, 32),
        };
        Controls.Add(approveButton);
        Controls.Add(denyButton);
        AcceptButton = approveButton;
        CancelButton = denyButton;

        _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        _countdownLabel.Text = FormatCountdown(_remainingSeconds);
        if (_remainingSeconds <= 0)
        {
            _countdownTimer.Stop();
            DialogResult = DialogResult.No;
            Close();
        }
    }

    private static string FormatCountdown(int remainingSeconds) =>
        $"Auto-deny in {remainingSeconds}s if no response.";

    private void AddField(string label, string value, int y)
    {
        Controls.Add(new Label
        {
            Text = label,
            Location = new Point(16, y),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
        });
        Controls.Add(new Label
        {
            Text = value,
            Location = new Point(16, y + 18),
            AutoSize = true,
            MaximumSize = new Size(448, 0),
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _countdownTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
