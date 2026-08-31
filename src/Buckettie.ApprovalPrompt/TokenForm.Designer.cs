#nullable enable

namespace Buckettie.ApprovalPrompt;

partial class TokenForm
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel rootLayout = null!;
    private Label instructionLabel = null!;
    private Label sourceProjectLabel = null!;
    private TextBox sourceProjectTextBox = null!;
    private Label targetUrlLabel = null!;
    private TextBox targetUrlTextBox = null!;
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
        sourceProjectLabel = new Label();
        sourceProjectTextBox = new TextBox();
        targetUrlLabel = new Label();
        targetUrlTextBox = new TextBox();
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
        rootLayout.Controls.Add(sourceProjectLabel, 0, 1);
        rootLayout.Controls.Add(sourceProjectTextBox, 0, 2);
        rootLayout.Controls.Add(targetUrlLabel, 0, 3);
        rootLayout.Controls.Add(targetUrlTextBox, 0, 4);
        rootLayout.Controls.Add(tokenLabel, 0, 5);
        rootLayout.Controls.Add(tokenTextBox, 0, 6);
        rootLayout.Controls.Add(buttonLayout, 0, 7);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Padding = new Padding(16);
        rootLayout.RowCount = 8;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        instructionLabel.AutoSize = true;
        instructionLabel.Dock = DockStyle.Fill;
        instructionLabel.Margin = new Padding(0, 0, 0, 16);
        sourceProjectLabel.AutoSize = true;
        sourceProjectLabel.Margin = new Padding(0, 0, 0, 4);
        sourceProjectTextBox.Dock = DockStyle.Top;
        sourceProjectTextBox.Margin = new Padding(0, 0, 0, 12);
        sourceProjectTextBox.ReadOnly = true;
        targetUrlLabel.AutoSize = true;
        targetUrlLabel.Margin = new Padding(0, 0, 0, 4);
        targetUrlTextBox.Dock = DockStyle.Top;
        targetUrlTextBox.Margin = new Padding(0, 0, 0, 12);
        targetUrlTextBox.ReadOnly = true;
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
        ClientSize = new Size(620, 310);
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
