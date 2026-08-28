#nullable enable

namespace Buckettie.ApprovalPrompt;

partial class TokenForm
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel rootLayout = null!;
    private Label instructionLabel = null!;
    private Label tokenLabel = null!;
    private TextBox tokenTextBox = null!;
    private FlowLayoutPanel buttonLayout = null!;
    private Button okButton = null!;
    private Button cancelButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        rootLayout = new TableLayoutPanel();
        instructionLabel = new Label();
        tokenLabel = new Label();
        tokenTextBox = new TextBox();
        buttonLayout = new FlowLayoutPanel();
        okButton = new Button();
        cancelButton = new Button();
        rootLayout.SuspendLayout();
        buttonLayout.SuspendLayout();
        SuspendLayout();
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(instructionLabel, 0, 0);
        rootLayout.Controls.Add(tokenLabel, 0, 1);
        rootLayout.Controls.Add(tokenTextBox, 0, 2);
        rootLayout.Controls.Add(buttonLayout, 0, 3);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Padding = new Padding(16);
        rootLayout.RowCount = 4;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        instructionLabel.AutoSize = true;
        instructionLabel.Dock = DockStyle.Fill;
        instructionLabel.Margin = new Padding(0, 0, 0, 16);
        tokenLabel.AutoSize = true;
        tokenLabel.Margin = new Padding(0, 0, 0, 4);
        tokenTextBox.Dock = DockStyle.Top;
        tokenTextBox.Margin = new Padding(0, 0, 0, 16);
        tokenTextBox.UseSystemPasswordChar = true;
        buttonLayout.AutoSize = true;
        buttonLayout.Controls.Add(okButton);
        buttonLayout.Controls.Add(cancelButton);
        buttonLayout.Dock = DockStyle.Bottom;
        buttonLayout.FlowDirection = FlowDirection.RightToLeft;
        buttonLayout.Margin = new Padding(0);
        okButton.DialogResult = DialogResult.OK;
        okButton.Margin = new Padding(8, 0, 0, 0);
        okButton.Size = new Size(96, 32);
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Margin = new Padding(8, 0, 0, 0);
        cancelButton.Size = new Size(96, 32);
        AcceptButton = okButton;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = cancelButton;
        ClientSize = new Size(520, 190);
        Controls.Add(rootLayout);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        rootLayout.ResumeLayout(false);
        rootLayout.PerformLayout();
        buttonLayout.ResumeLayout(false);
        ResumeLayout(false);
    }
}
